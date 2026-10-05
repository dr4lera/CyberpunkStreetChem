#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <cstdint>
#include <cstring>
#include <vector>
#include <string>

namespace sc {
constexpr uint32_t magic = 0x57575054, version = 1;
constexpr uint32_t max_width = 3840, max_height = 2160;
constexpr size_t capacity = size_t(max_width) * max_height * 4;
constexpr wchar_t bus_name[] = L"Local\\StreetChem.Render.v1";
struct Header {
    uint32_t signature, schema, width, height, pitch, bytes, producer_pid, reserved;
    uint64_t sequence, timestamp_ms;
};
struct Frame {
    Header header{};
    std::vector<uint8_t> rgba;
};
// A bounded, nonblocking diagnostic transport. It carries actual rendered pixels,
// not weapon simulations or a fabricated alpha mask. Neither side waits on the other.
class Bus {
    HANDLE mapping_ = nullptr, mutex_ = nullptr;
    uint8_t* view_ = nullptr;
    bool writer_ = false;
    bool lock() {
        const auto result = WaitForSingleObject(mutex_, 0);
        return result == WAIT_OBJECT_0 || result == WAIT_ABANDONED;
    }
public:
    Bus() = default;
    Bus(const Bus&) = delete;
    Bus& operator=(const Bus&) = delete;
    ~Bus() { close(); }
    void close() {
        if (view_) UnmapViewOfFile(view_);
        if (mapping_) CloseHandle(mapping_);
        if (mutex_) CloseHandle(mutex_);
        view_ = nullptr; mapping_ = mutex_ = nullptr;
    }
    bool open(bool writer, const wchar_t* name = bus_name) {
        close(); writer_ = writer;
        std::wstring mutex_name = std::wstring(name) + L".Lock";
        mutex_ = CreateMutexW(nullptr, FALSE, mutex_name.c_str());
        mapping_ = writer ? CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr,
            PAGE_READWRITE, 0, DWORD(sizeof(Header) + capacity), name)
            : OpenFileMappingW(FILE_MAP_READ, FALSE, name);
        if (!mutex_ || !mapping_) { close(); return false; }
        view_ = static_cast<uint8_t*>(MapViewOfFile(mapping_, writer ? FILE_MAP_WRITE : FILE_MAP_READ,
            0, 0, sizeof(Header) + capacity));
        if (!view_) { close(); return false; }
        return true;
    }
    bool publish(const uint8_t* pixels, uint32_t w, uint32_t h, uint32_t row_pitch,
                 bool bgra = false) {
        if (!writer_ || !view_ || !pixels || !w || !h || w > max_width || h > max_height
            || row_pitch < w * 4 || !lock()) return false;
        auto* head = reinterpret_cast<Header*>(view_);
        const auto now = GetTickCount64();
        if (head->signature == magic && head->producer_pid != GetCurrentProcessId()
            && now >= head->timestamp_ms && now - head->timestamp_ms < 3000) {
            ReleaseMutex(mutex_); return false;
        }
        const uint64_t sequence = head->signature == magic ? head->sequence + 1 : 1;
        auto* out = view_ + sizeof(Header);
        for (uint32_t y = 0; y < h; ++y) {
            const auto* row = pixels + size_t(y) * row_pitch;
            if (!bgra) std::memcpy(out + size_t(y) * w * 4, row, size_t(w) * 4);
            else for (uint32_t x = 0; x < w; ++x) {
                auto* dst = out + (size_t(y) * w + x) * 4;
                dst[0] = row[x * 4 + 2]; dst[1] = row[x * 4 + 1];
                dst[2] = row[x * 4]; dst[3] = row[x * 4 + 3];
            }
        }
        *head = { magic, version, w, h, w * 4, w * h * 4,
                  GetCurrentProcessId(), 0, sequence, now };
        ReleaseMutex(mutex_); return true;
    }
    bool read(Frame& frame, uint64_t last_sequence = 0, uint64_t max_age_ms = 3000) {
        if (writer_ || !view_ || !lock()) return false;
        Header h{}; std::memcpy(&h, view_, sizeof(h));
        const uint64_t now = GetTickCount64();
        const bool valid = h.signature == magic && h.schema == version && h.width && h.height
            && h.width <= max_width && h.height <= max_height && h.pitch == h.width * 4
            && h.bytes == h.width * h.height * 4 && h.sequence != last_sequence
            && now >= h.timestamp_ms && now - h.timestamp_ms <= max_age_ms;
        if (valid) {
            frame.rgba.resize(h.bytes);
            std::memcpy(frame.rgba.data(), view_ + sizeof(Header), h.bytes);
            frame.header = h;
        }
        ReleaseMutex(mutex_); return valid;
    }
};
}
