#pragma once
#include "../generated/input_config.hpp"
#include <atomic>
struct SCInputPacket {
    uint32_t magic,version;
    volatile LONG64 sequence;
    uint64_t timestamp,keys;
    float x,y,dx,dy,wheel;
    uint32_t buttons,active,reserved;
};
static_assert(sizeof(SCInputPacket)==64);
class InputBus {
    HANDLE mapping=nullptr;
    SCInputPacket* packet=nullptr;
    HWND window=nullptr;
    WNDPROC original=nullptr;
    static inline std::atomic<LONG> mouseX=0,mouseY=0;
    static inline WNDPROC chain=nullptr;
    static LRESULT CALLBACK InputProc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp) {
        if(msg==WM_INPUT) {
            RAWINPUT raw{};UINT size=sizeof(raw);
            if(GetRawInputData((HRAWINPUT)lp,RID_INPUT,&raw,&size,sizeof(RAWINPUTHEADER))!=(UINT)-1 && raw.header.dwType==RIM_TYPEMOUSE && !(raw.data.mouse.usFlags&MOUSE_MOVE_ABSOLUTE)) {
                mouseX.fetch_add(raw.data.mouse.lLastX);mouseY.fetch_add(raw.data.mouse.lLastY);
            }
        }
        return CallWindowProcW(chain,hwnd,msg,wp,lp);
    }
public:
    ~InputBus(){close();}
    void open(HWND hwnd) {
        if(packet) return;
        window=hwnd;
        mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,64,sc_input_mapping);
        if(!mapping) return;
        packet=(SCInputPacket*)MapViewOfFile(mapping,FILE_MAP_WRITE,0,0,64);
        if(!packet){CloseHandle(mapping);mapping=nullptr;return;}
        packet->magic=sc_input_magic;packet->version=1;packet->sequence=0;packet->active=0;
        original=(WNDPROC)SetWindowLongPtrW(window,GWLP_WNDPROC,(LONG_PTR)InputProc);chain=original;
    }
    void close(){
        if(packet){packet->active=0;UnmapViewOfFile(packet);packet=nullptr;}
        if(mapping){CloseHandle(mapping);mapping=nullptr;}
        if(original && (WNDPROC)GetWindowLongPtrW(window,GWLP_WNDPROC)==InputProc) SetWindowLongPtrW(window,GWLP_WNDPROC,(LONG_PTR)original);
        original=nullptr;window=nullptr;
    }
    void write(reshade::api::effect_runtime* runtime,bool active) {
        if(!packet)open((HWND)runtime->get_hwnd());
        if(!packet)return;
        active=active && GetForegroundWindow()==window;
        uint64_t keys=0;
        if(active)for(size_t i=0;i<std::size(sc_keys);i++)if(runtime->is_key_down(sc_keys[i]))keys|=uint64_t(1)<<i;
        uint32_t x=0,y=0;int16_t wheel=0;runtime->get_mouse_cursor_position(&x,&y,&wheel);
        POINT point{(LONG)x,(LONG)y};ScreenToClient(window,&point);RECT rect{};GetClientRect(window,&rect);
        const LONG dx=mouseX.exchange(0),dy=mouseY.exchange(0);
        InterlockedIncrement64(&packet->sequence);
        packet->timestamp=GetTickCount64();packet->keys=keys;
        packet->x=rect.right>0?float(point.x)/rect.right:0.5f;
        packet->y=rect.bottom>0?1.0f-float(point.y)/rect.bottom:0.5f;
        packet->dx=active?float(dx):0;packet->dy=active?float(-dy):0;
        packet->wheel=active?float(wheel)*120.0f:0;
        packet->buttons=active?((runtime->is_mouse_button_down(0)?1:0)|(runtime->is_mouse_button_down(2)?2:0)|(runtime->is_mouse_button_down(1)?4:0)):0;
        packet->active=active?1:0;
        MemoryBarrier();InterlockedIncrement64(&packet->sequence);
        if(active)runtime->block_input_next_frame();
    }
};
