#pragma once
#include <filesystem>
#include <fstream>
#include <cmath>
#include <random>
namespace scstore {
using Data=nlohmann::json;
inline Data data=Data::object();
inline std::filesystem::path path;
inline bool writable=false;
inline int NewKey(){static std::random_device random;return static_cast<int>(random()&0x7fffffffu)|1;}
inline int Load(int key,const std::string& session){
    writable=false;
    if(key<=0||session.empty())return -1;
    wchar_t local[32768]{};
    if(!GetEnvironmentVariableW(L"LOCALAPPDATA",local,32768))return -1;
    path=std::filesystem::path(local)/L"StreetChem"/L"worlds"/(std::to_wstring(key)+L".json");
    data={{"schema",1},{"world",key},{"session",session},{"placements",Data::array()}};
    try {
        if(!std::filesystem::exists(path)){writable=true;return 0;}
        if(std::filesystem::file_size(path)>65536)return -1;
        std::ifstream input(path);auto loaded=Data::parse(input);
        if(loaded.value("schema",0)!=1||loaded.value("world",0)!=key||loaded.value("session",std::string())!=session||!loaded["placements"].is_array()||loaded["placements"].size()>64)return -1;
        for(const auto& p:loaded["placements"]){
            if(p.value("guid",std::string()).size()!=36)return -1;
            for(const char* axis:{"x","y","z","yaw"})if(!p.contains(axis)||!p[axis].is_number()||!std::isfinite(p[axis].get<float>())||std::abs(p[axis].get<float>())>2000000)return -1;
        }
        data=std::move(loaded);writable=true;return 1;
    }catch(...){return -1;}
}
inline bool Save(){
    if(!writable)return false;
    try {
        std::filesystem::create_directories(path.parent_path());
        auto temp=path;temp+=L".tmp";
        {std::ofstream output(temp,std::ios::binary|std::ios::trunc);output<<data.dump(2);output.flush();if(!output)return false;}
        return MoveFileExW(temp.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH)!=FALSE;
    }catch(...){return false;}
}
inline bool Put(const std::string& guid,const RED4ext::Vector4& p,float yaw){
    if(!writable||guid.size()!=36||!std::isfinite(p.X)||!std::isfinite(p.Y)||!std::isfinite(p.Z)||!std::isfinite(yaw))return false;
    auto old=data;
    auto& rows=data["placements"];
    Data row={{"guid",guid},{"x",p.X},{"y",p.Y},{"z",p.Z},{"yaw",yaw}};
    bool found=false;
    for(auto& entry:rows)if(entry["guid"]==guid){entry=row;found=true;break;}
    if(!found){if(rows.size()>=64)return false;rows.push_back(row);}
    if(Save())return true;data=std::move(old);return false;
}
inline bool Remove(const std::string& guid){
    if(!writable)return false;
    auto old=data;auto& rows=data["placements"];
    for(auto it=rows.begin();it!=rows.end();)if((*it)["guid"]==guid)it=rows.erase(it);else ++it;
    if(Save())return true;data=std::move(old);return false;
}
inline Data Row(int index){if(!writable||index<0||index>=static_cast<int>(data["placements"].size()))return Data::object();return data["placements"][index];}
}
