#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <RED4ext/RED4ext.hpp>
#include <RED4ext/CString.hpp>
#include <RED4ext/Scripting/Natives/Vector4.hpp>
#include <json.hpp>
#include <thread>
#include <mutex>
#include <atomic>
#include <deque>
#include <array>
#include <map>
#include <unordered_map>
#include <chrono>
#include <string>
#include "world_store.hpp"
using Json=nlohmann::json;
namespace {
std::atomic_bool running=false;
std::thread worker;
std::thread keyboardWorker;
std::mutex guard;
std::deque<Json> outgoing;
Json snapshot=Json::object(), receipt=Json::object();
std::unordered_map<std::string,Json> replies;
uint64_t lastSuccess=0;
std::map<std::string,Json> doseCatalog;
HANDLE drugMapping=nullptr;
uint8_t* drugView=nullptr;
std::filesystem::path CatalogPath(){wchar_t local[32768]{};GetEnvironmentVariableW(L"LOCALAPPDATA",local,32768);return std::filesystem::path(local)/L"StreetChem"/L"consumables"/L"catalog.json";}
void LoadCatalog(){try{std::ifstream file(CatalogPath());if(file){Json data;file>>data;for(auto& [key,row]:data.items())doseCatalog[key]=row;}}catch(...){} }
void RememberProducts(const Json& data){
    bool changed=false;
    for(const auto& collection:{"items","shop"})if(data.contains(collection))for(const auto& item:data[collection]){
        if(!item.value("product",false)||!item.contains("key")||!item["key"].is_string())continue;
        const auto key=item["key"].get<std::string>();if(key.size()!=24)continue;
        Json row=item;row.erase("slot");row.erase("quantity");row.erase("price");
        if(std::string(collection)=="items" && item.value("quantity",0)>0)row["sellPrice"]=static_cast<int>(item.value("price",0.0)/item.value("quantity",1)*10.0);
        else if(doseCatalog.contains(key) && doseCatalog[key].contains("sellPrice"))row["sellPrice"]=doseCatalog[key]["sellPrice"];
        else row["sellPrice"]=static_cast<int>(item.value("price",0.0)/1.25);
        if(!doseCatalog.contains(key)||doseCatalog[key]!=row){doseCatalog[key]=row;changed=true;}
    }
    if(changed)try{const auto path=CatalogPath();std::filesystem::create_directories(path.parent_path());auto tmp=path;tmp+=L".tmp";{std::ofstream file(tmp);file<<Json(doseCatalog).dump(2);}MoveFileExW(tmp.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING);}catch(...){}
}
std::array<std::atomic_bool,256> pressed{};
std::array<bool,256> previous{};
std::array<std::atomic_int,256> modifiers{};
HANDLE poseMapping=nullptr;
uint8_t* poseView=nullptr;
bool Focused(){DWORD pid=0;GetWindowThreadProcessId(GetForegroundWindow(),&pid);return pid==GetCurrentProcessId();}
Json Exchange(const Json& request) {
    HANDLE pipe=CreateFileW(L"\\\\.\\pipe\\StreetChem.Control.v1",GENERIC_READ|GENERIC_WRITE,0,nullptr,OPEN_EXISTING,FILE_FLAG_OVERLAPPED,nullptr);
    if(pipe==INVALID_HANDLE_VALUE)throw std::runtime_error("guest_unavailable");
    HANDLE event=CreateEventW(nullptr,TRUE,FALSE,nullptr);
    auto cleanup=[&]{CancelIoEx(pipe,nullptr);CloseHandle(pipe);CloseHandle(event);};
    auto io=[&](bool write,void* data,DWORD size)->DWORD {
        ResetEvent(event);OVERLAPPED ov{};ov.hEvent=event;DWORD transferred=0;
        BOOL ok=write?WriteFile(pipe,data,size,&transferred,&ov):ReadFile(pipe,data,size,&transferred,&ov);
        if(!ok && GetLastError()!=ERROR_IO_PENDING)throw std::runtime_error("pipe_io_failed");
        if(!ok){
            if(WaitForSingleObject(event,1500)!=WAIT_OBJECT_0){CancelIoEx(pipe,&ov);WaitForSingleObject(event,INFINITE);throw std::runtime_error("guest_timeout");}
            if(!GetOverlappedResult(pipe,&ov,&transferred,FALSE))throw std::runtime_error("pipe_io_failed");
        }
        return transferred;
    };
    try {
        std::string text=request.dump()+"\n";
        if(io(true,text.data(),static_cast<DWORD>(text.size()))!=text.size())throw std::runtime_error("partial_request");
        text.clear();char buffer[4096];
        while(text.find('\n')==std::string::npos && text.size()<65536){auto n=io(false,buffer,sizeof(buffer));if(!n)throw std::runtime_error("pipe_closed");text.append(buffer,n);}
        auto reply=Json::parse(text);cleanup();return reply;
    }catch(...){cleanup();throw;}
}
void Run() {
    uint64_t nextObserve=0;
    while(running) {
        Json request;
        {
            std::lock_guard lock(guard);
            if(!outgoing.empty()){request=outgoing.front();outgoing.pop_front();}
        }
        const auto now=GetTickCount64();
        if(request.is_null() && now>=nextObserve){request={{"op","observe"}};nextObserve=now+500;}
        if(!request.is_null()) {
            try {
                auto response=Exchange(request);
                if(!response.value("ok",false))response["status"]="error";
                if(request.contains("id"))response["id"]=request["id"];
                std::lock_guard lock(guard);lastSuccess=now;
                if(request["op"]=="observe"){RememberProducts(response);snapshot=std::move(response);}
                else {receipt=response;if(request.contains("id")){if(replies.size()>512)replies.erase(replies.begin());replies[request["id"].get<std::string>()]=std::move(response);}}
            }catch(const std::exception& e){
                if(request.contains("id")){std::lock_guard lock(guard);receipt={{"ok",false},{"status","uncertain"},{"id",request["id"]},{"error",e.what()}};replies[request["id"].get<std::string>()]=receipt;}
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
}
void RunKeys(){while(running){
    const bool focused=Focused();
    const int mask=((GetAsyncKeyState(VK_CONTROL)&0x8000)?1:0)|((GetAsyncKeyState(VK_SHIFT)&0x8000)?2:0)|((GetAsyncKeyState(VK_MENU)&0x8000)?4:0);
    for(int key=1;key<256;key++){const bool down=focused&&(GetAsyncKeyState(key)&0x8000)!=0;if(down&&!previous[key]){modifiers[key]=mask;pressed[key]=true;}previous[key]=down;}
    std::this_thread::sleep_for(std::chrono::milliseconds(5));
}}
Json Product(int index){
    int current=0;
    if(!snapshot.contains("items"))return Json::object();
    for(const auto& item:snapshot["items"])if(item.value("product",false)){if(current++==index)return item;}
    return Json::object();
}
Json Runner(int index){if(index<0 || !snapshot.contains("runners") || index>=static_cast<int>(snapshot["runners"].size()))return Json::object();return snapshot["runners"][index];}
using Context=RED4ext::IScriptable;using Frame=RED4ext::CStackFrame;
Json Dose(int index){if(index<0||index>=static_cast<int>(doseCatalog.size()))return Json::object();auto it=doseCatalog.begin();std::advance(it,index);return it->second;}
void Clock(Context*,Frame* f,float* out,int64_t){f->code++;static const auto start=GetTickCount64();if(out)*out=static_cast<float>(GetTickCount64()-start)/1000.f;}
void ShopCount(Context*,Frame* f,int32_t* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=snapshot.contains("shop")?static_cast<int32_t>(snapshot["shop"].size()):0;}
void ShopValue(Context*,Frame* f,RED4ext::CString* out,int64_t){int32_t index=0;RED4ext::CString field;RED4ext::GetParameter(f,&index);RED4ext::GetParameter(f,&field);f->code++;std::lock_guard lock(guard);std::string value;
    if(snapshot.contains("shop") && index>=0 && index<static_cast<int32_t>(snapshot["shop"].size())){const auto& row=snapshot["shop"][index];if(row.contains(field.c_str())){auto item=row[field.c_str()];value=item.is_string()?item.get<std::string>():item.dump();}}if(out)*out=RED4ext::CString(value);
}
void DoseCount(Context*,Frame* f,int32_t* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=static_cast<int32_t>(doseCatalog.size());}
void DoseValue(Context*,Frame* f,RED4ext::CString* out,int64_t){int32_t index=0;RED4ext::CString field;RED4ext::GetParameter(f,&index);RED4ext::GetParameter(f,&field);f->code++;std::lock_guard lock(guard);const auto dose=Dose(index);std::string value;
    if(field=="quantity"){int64_t count=0;if(snapshot.contains("items"))for(const auto& item:snapshot["items"])if(item.value("product",false)&&item.value("key",std::string())==dose.value("key",std::string()))count+=item.value("quantity",0);value=std::to_string(std::clamp<int64_t>(count,0,INT32_MAX));}
    else if(field=="sellPrice"){value=std::to_string(dose.value("sellPrice",0));if(snapshot.contains("items"))for(const auto& item:snapshot["items"])if(item.value("product",false) && item.value("key",std::string())==dose.value("key",std::string()) && item.value("quantity",0)>0){value=std::to_string(static_cast<int>(item.value("price",0.0)/item.value("quantity",1)*10.0));break;}}
    else if(dose.contains(field.c_str())){auto item=dose[field.c_str()];value=item.is_string()?item.get<std::string>():item.dump();}if(out)*out=RED4ext::CString(value);
}
void Consume(Context*,Frame* f,bool* out,int64_t){RED4ext::CString id,key;RED4ext::GetParameter(f,&id);RED4ext::GetParameter(f,&key);f->code++;std::lock_guard lock(guard);if(out)*out=false;if(outgoing.size()>=32||!doseCatalog.contains(key.c_str()))return;
    outgoing.push_back({{"op","consume"},{"id",id.c_str()},{"key",key.c_str()},{"session",snapshot.value("session",std::string())}});replies.erase(id.c_str());if(out)*out=true;
}
void DrugVisual(Context*,Frame* f,void*,int64_t){float saturation=1,vignette=0;RED4ext::GetParameter(f,&saturation);RED4ext::GetParameter(f,&vignette);f->code++;
    if(!drugView){drugMapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,32,L"Local\\StreetChem.Drug.v1");if(drugMapping)drugView=static_cast<uint8_t*>(MapViewOfFile(drugMapping,FILE_MAP_WRITE,0,0,32));}if(!drugView)return;
    auto seq=reinterpret_cast<volatile LONG*>(drugView+4);InterlockedIncrement(seq);*reinterpret_cast<uint32_t*>(drugView)=0x53434452;*reinterpret_cast<uint64_t*>(drugView+8)=GetTickCount64();*reinterpret_cast<float*>(drugView+16)=saturation;*reinterpret_cast<float*>(drugView+20)=vignette;MemoryBarrier();InterlockedIncrement(seq);
}
void Report(Context*,Frame* f,void*,int64_t){
    RED4ext::CString report;RED4ext::GetParameter(f,&report);f->code++;
    static uint64_t next=0;if(GetTickCount64()<next)return;next=GetTickCount64()+1000;
    try {auto row=Json::parse(report.c_str());row["pid"]=GetCurrentProcessId();row["world"]=scstore::data.value("world",0);row["tick"]=GetTickCount64();
        wchar_t local[32768]{};if(!GetEnvironmentVariableW(L"LOCALAPPDATA",local,32768))return;
        auto dir=std::filesystem::path(local)/L"StreetChem";std::filesystem::create_directories(dir);
        auto path=dir/L"host-status.json";auto temp=dir/L"host-status.tmp";
        {std::ofstream output(temp);output<<row.dump(2);}MoveFileExW(temp.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING);
    }catch(...){}
}
void WorldNew(Context*,Frame* f,int32_t* out,int64_t){f->code++;if(out)*out=scstore::NewKey();}
void WorldLoad(Context*,Frame* f,int32_t* out,int64_t){int32_t key=0;RED4ext::CString session;RED4ext::GetParameter(f,&key);RED4ext::GetParameter(f,&session);f->code++;int result=scstore::Load(key,session.c_str());if(out)*out=result;}
void WorldCount(Context*,Frame* f,int32_t* out,int64_t){f->code++;if(out)*out=scstore::writable?static_cast<int32_t>(scstore::data["placements"].size()):0;}
void WorldGUID(Context*,Frame* f,RED4ext::CString* out,int64_t){int32_t i=0;RED4ext::GetParameter(f,&i);f->code++;if(out)*out=RED4ext::CString(scstore::Row(i).value("guid",std::string()));}
void WorldPosition(Context*,Frame* f,RED4ext::Vector4* out,int64_t){int32_t i=0;RED4ext::GetParameter(f,&i);f->code++;auto row=scstore::Row(i);if(out){out->X=row.value("x",0.f);out->Y=row.value("y",0.f);out->Z=row.value("z",0.f);out->W=1.f;}}
void WorldYaw(Context*,Frame* f,float* out,int64_t){int32_t i=0;RED4ext::GetParameter(f,&i);f->code++;if(out)*out=scstore::Row(i).value("yaw",0.f);}
void WorldPut(Context*,Frame* f,bool* out,int64_t){RED4ext::CString guid;RED4ext::Vector4 p{};float yaw=0;RED4ext::GetParameter(f,&guid);RED4ext::GetParameter(f,&p);RED4ext::GetParameter(f,&yaw);f->code++;bool result=scstore::Put(guid.c_str(),p,yaw);if(out)*out=result;}
void WorldRemove(Context*,Frame* f,bool* out,int64_t){RED4ext::CString guid;RED4ext::GetParameter(f,&guid);f->code++;bool result=scstore::Remove(guid.c_str());if(out)*out=result;}
void Hotkey(Context*,Frame* f,bool* out,int64_t){int32_t key=0,mask=0;RED4ext::GetParameter(f,&key);RED4ext::GetParameter(f,&mask);f->code++;if(out)*out=key>0&&key<256&&Focused()&&modifiers[key]==mask&&pressed[key].exchange(false);}
void Request(Context*,Frame* f,bool* out,int64_t){
    RED4ext::CString op,id,payload;RED4ext::GetParameter(f,&op);RED4ext::GetParameter(f,&id);RED4ext::GetParameter(f,&payload);f->code++;if(out)*out=false;
    try {if(strlen(payload.c_str())>16384)return;Json request=Json::parse(payload.c_str());if(!request.is_object())return;request["op"]=op.c_str();request["id"]=id.c_str();
        std::lock_guard lock(guard);if(outgoing.size()>=32)return;if(std::string(op.c_str()).starts_with("buy_"))request["session"]=snapshot.value("session",std::string());replies.erase(id.c_str());outgoing.push_back(std::move(request));if(out)*out=true;
    }catch(const std::exception&){}
}
void Value(Context*,Frame* f,RED4ext::CString* out,int64_t){RED4ext::CString id,key;RED4ext::GetParameter(f,&id);RED4ext::GetParameter(f,&key);f->code++;std::lock_guard lock(guard);std::string value;
    auto it=replies.find(id.c_str());if(it!=replies.end()&&it->second.contains(key.c_str())){auto field=it->second[key.c_str()];value=field.is_string()?field.get<std::string>():field.dump();}if(out)*out=RED4ext::CString(value);
}
void Session(Context*,Frame* f,RED4ext::CString* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=RED4ext::CString(snapshot.value("session",std::string("")));}
void Camera(Context*,Frame* f,void*,int64_t){
    RED4ext::Vector4 position{},forward{},up{};float fov=0,aspect=0;
    RED4ext::GetParameter(f,&position);RED4ext::GetParameter(f,&forward);RED4ext::GetParameter(f,&up);RED4ext::GetParameter(f,&fov);RED4ext::GetParameter(f,&aspect);f->code++;
    if(!poseView){poseMapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,80,L"Local\\StreetChem.Pose.v1");if(poseMapping)poseView=static_cast<uint8_t*>(MapViewOfFile(poseMapping,FILE_MAP_WRITE,0,0,80));}
    if(!poseView)return;
    auto sequence=reinterpret_cast<volatile LONG*>(poseView+8);InterlockedIncrement(sequence);
    *reinterpret_cast<uint32_t*>(poseView)=0x5343504F;*reinterpret_cast<uint32_t*>(poseView+4)=1;
    *reinterpret_cast<int32_t*>(poseView+12)=scstore::data.value("world",0);
    *reinterpret_cast<uint64_t*>(poseView+16)=GetTickCount64();
    memcpy(poseView+24,&position,12);memcpy(poseView+36,&forward,12);memcpy(poseView+48,&up,12);
    memcpy(poseView+60,&fov,4);memcpy(poseView+64,&aspect,4);MemoryBarrier();InterlockedIncrement(sequence);
}
void Visual(Context*,Frame* f,bool* out,int64_t){
    RED4ext::CString equipment;RED4ext::Vector4 position{};float yaw=0;
    RED4ext::GetParameter(f,&equipment);RED4ext::GetParameter(f,&position);RED4ext::GetParameter(f,&yaw);f->code++;
    std::lock_guard lock(guard);if(out)*out=false;if(outgoing.size()>=32)return;
    outgoing.push_back({{"op","world_visual"},{"equipment",equipment.c_str()},{"position",{{"x",position.X},{"y",position.Y},{"z",position.Z}}},{"yaw",yaw}});if(out)*out=true;
}
void Key(Context*,Frame* f,bool* out,int64_t){int32_t key=0;RED4ext::GetParameter(f,&key);f->code++;if(out)*out=key>0&&key<256&&pressed[key].exchange(false)&&Focused();}
void Count(Context*,Frame* f,int32_t* out,int64_t){f->code++;std::lock_guard lock(guard);int count=0;if(snapshot.contains("items"))for(auto& p:snapshot["items"])if(p.value("product",false))++count;if(out)*out=count;}
void Connected(Context*,Frame* f,bool* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=lastSuccess && GetTickCount64()-lastSuccess<2000 && snapshot.value("loaded",false);}
void ProductName(Context*,Frame* f,RED4ext::CString* out,int64_t){int32_t index=0;RED4ext::GetParameter(f,&index);f->code++;std::lock_guard lock(guard);if(out)*out=RED4ext::CString(Product(index).value("name",std::string("")));}
void Quantity(Context*,Frame* f,int32_t* out,int64_t){int32_t index=0;RED4ext::GetParameter(f,&index);f->code++;std::lock_guard lock(guard);if(out)*out=Product(index).value("quantity",0);}
void ProductSlot(Context*,Frame* f,int32_t* out,int64_t){int32_t index=0;RED4ext::GetParameter(f,&index);f->code++;std::lock_guard lock(guard);if(out)*out=Product(index).value("slot",-1);}
void RunnerCount(Context*,Frame* f,int32_t* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=snapshot.contains("runners")?static_cast<int32_t>(snapshot["runners"].size()):0;}
void RunnerValue(Context*,Frame* f,RED4ext::CString* out,int64_t){int32_t index=0;RED4ext::CString key;RED4ext::GetParameter(f,&index);RED4ext::GetParameter(f,&key);f->code++;std::lock_guard lock(guard);auto row=Runner(index);std::string result;if(row.contains(key.c_str())){auto value=row[key.c_str()];result=value.is_string()?value.get<std::string>():value.dump();}if(out)*out=RED4ext::CString(result);}
void Price(Context*,Frame* f,int32_t* out,int64_t){int32_t index=0;RED4ext::GetParameter(f,&index);f->code++;std::lock_guard lock(guard);auto p=Product(index);const int q=p.value("quantity",0);if(out)*out=q>0?static_cast<int>(p.value("price",0.0)/q*10.0):0;}
void Status(Context*,Frame* f,RED4ext::CString* out,int64_t){RED4ext::CString id;RED4ext::GetParameter(f,&id);f->code++;std::lock_guard lock(guard);auto it=replies.find(id.c_str());if(out)*out=RED4ext::CString(it==replies.end()?"":it->second.value("status",std::string()));}
void Error(Context*,Frame* f,RED4ext::CString* out,int64_t){f->code++;std::lock_guard lock(guard);if(out)*out=RED4ext::CString(receipt.value("detail",receipt.value("error",std::string(""))));}
void NewID(Context*,Frame* f,RED4ext::CString* out,int64_t){f->code++;static std::atomic<uint64_t> counter=0;if(out)*out=RED4ext::CString(std::to_string(GetCurrentProcessId())+"-"+std::to_string(GetTickCount64())+"-"+std::to_string(++counter));}
void Sale(Context*,Frame* f,bool* out,int64_t){
    RED4ext::CString op,id;int32_t index=0,qty=0;
    RED4ext::GetParameter(f,&op);RED4ext::GetParameter(f,&id);RED4ext::GetParameter(f,&index);RED4ext::GetParameter(f,&qty);f->code++;
    std::lock_guard lock(guard);if(out)*out=false;if(outgoing.size()>=32)return;
    auto p=Product(index);
    Json request={{"op",op.c_str()},{"id",id.c_str()}};
    if(op=="reserve"){
        if(p.empty())return;
        request["slot"]=p["slot"];request["product"]=p["id"];request["quantity"]=qty;request["session"]=snapshot.value("session",std::string(""));
    }else if(op!="commit"&&op!="abort")return;
    replies.erase(id.c_str());outgoing.push_back(std::move(request));if(out)*out=true;
}
void RunnerReserve(Context*,Frame* f,bool* out,int64_t){
    RED4ext::CString id,dealer;RED4ext::GetParameter(f,&id);RED4ext::GetParameter(f,&dealer);f->code++;
    std::lock_guard lock(guard);if(out)*out=false;if(outgoing.size()>=32)return;
    Json request={{"op","reserve"},{"id",id.c_str()},{"dealer",dealer.c_str()},{"session",snapshot.value("session",std::string(""))}};
    replies.erase(id.c_str());outgoing.push_back(std::move(request));if(out)*out=true;
}
void Register() {
    auto rtti=RED4ext::CRTTISystem::Get();
    auto add=[&](const char* name,auto callback,const char* returns,std::initializer_list<std::pair<const char*,const char*>> params={}) {
        auto function=RED4ext::CGlobalFunction::Create(name,name,callback);function->flags={.isNative=true,.isStatic=true};function->SetReturnType(returns);
        for(auto& p:params)function->AddParam(p.first,p.second);
        rtti->RegisterFunction(function);
    };
    add("SC_Key",Key,"Bool",{{"Int32","key"}});add("SC_Count",Count,"Int32");add("SC_Connected",Connected,"Bool");
    add("SC_Report",Report,"Void",{{"String","report"}});
    add("SC_Clock",Clock,"Float");add("SC_ShopCount",ShopCount,"Int32");add("SC_ShopValue",ShopValue,"String",{{"Int32","index"},{"String","field"}});
    add("SC_DoseCount",DoseCount,"Int32");add("SC_DoseValue",DoseValue,"String",{{"Int32","index"},{"String","field"}});
    add("SC_Consume",Consume,"Bool",{{"String","id"},{"String","key"}});add("SC_DrugVisual",DrugVisual,"Void",{{"Float","saturation"},{"Float","vignette"}});
    add("SC_ProductSlot",ProductSlot,"Int32",{{"Int32","index"}});add("SC_RunnerCount",RunnerCount,"Int32");add("SC_RunnerValue",RunnerValue,"String",{{"Int32","index"},{"String","key"}});
    add("SC_Name",ProductName,"String",{{"Int32","index"}});add("SC_Quantity",Quantity,"Int32",{{"Int32","index"}});add("SC_Price",Price,"Int32",{{"Int32","index"}});
    add("SC_Status",Status,"String",{{"String","id"}});add("SC_Error",Error,"String");add("SC_NewID",NewID,"String");
    add("SC_Sale",Sale,"Bool",{{"String","op"},{"String","id"},{"Int32","index"},{"Int32","qty"}});
    add("SC_RunnerReserve",RunnerReserve,"Bool",{{"String","id"},{"String","dealer"}});
    add("SC_Camera",Camera,"Void",{{"Vector4","position"},{"Vector4","forward"},{"Vector4","up"},{"Float","fov"},{"Float","aspect"}});
    add("SC_Visual",Visual,"Bool",{{"String","equipment"},{"Vector4","position"},{"Float","yaw"}});
    add("SC_Hotkey",Hotkey,"Bool",{{"Int32","key"},{"Int32","modifiers"}});
    add("SC_Request",Request,"Bool",{{"String","op"},{"String","id"},{"String","payload"}});
    add("SC_Value",Value,"String",{{"String","id"},{"String","field"}});add("SC_Session",Session,"String");
    add("SC_WorldNew",WorldNew,"Int32");add("SC_WorldLoad",WorldLoad,"Int32",{{"Int32","world"},{"String","session"}});
    add("SC_WorldCount",WorldCount,"Int32");add("SC_WorldGUID",WorldGUID,"String",{{"Int32","index"}});
    add("SC_WorldPosition",WorldPosition,"Vector4",{{"Int32","index"}});add("SC_WorldYaw",WorldYaw,"Float",{{"Int32","index"}});
    add("SC_WorldPut",WorldPut,"Bool",{{"String","guid"},{"Vector4","position"},{"Float","yaw"}});add("SC_WorldRemove",WorldRemove,"Bool",{{"String","guid"}});
}
}
RED4EXT_C_EXPORT bool RED4EXT_CALL Main(RED4ext::v1::PluginHandle,RED4ext::v1::EMainReason reason,const RED4ext::v1::Sdk*){
    if(reason==RED4ext::v1::EMainReason::Load){LoadCatalog();RED4ext::CRTTISystem::Get()->AddPostRegisterCallback(Register);running=true;worker=std::thread(Run);keyboardWorker=std::thread(RunKeys);}
    if(reason==RED4ext::v1::EMainReason::Unload){running=false;if(worker.joinable())worker.join();if(keyboardWorker.joinable())keyboardWorker.join();if(poseView)UnmapViewOfFile(poseView);if(poseMapping)CloseHandle(poseMapping);if(drugView)UnmapViewOfFile(drugView);if(drugMapping)CloseHandle(drugMapping);}
    return true;
}
RED4EXT_C_EXPORT void RED4EXT_CALL Query(RED4ext::v1::PluginInfo* info){info->name=L"Street Chem Host";info->author=L"zrock / Codex";info->version=RED4EXT_V1_SEMVER(0,2,0);info->runtime=RED4EXT_V1_RUNTIME_VERSION_LATEST;info->sdk=RED4EXT_V1_SDK_VERSION_CURRENT;}
RED4EXT_C_EXPORT uint32_t RED4EXT_CALL Supports(){return RED4EXT_API_VERSION_1;}
