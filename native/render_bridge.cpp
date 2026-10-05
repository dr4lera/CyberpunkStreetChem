#include "render_bus.hpp"
#include <reshade.hpp>
#include <memory>
#include <unordered_map>
#include <cstdio>
#include "input_bus.hpp"

using namespace reshade::api;
namespace {
bool guest = false;
constexpr const char* effect = "StreetChemLab.fx";
struct State {
    InputBus input;
    sc::Bus bus;
    bool connected = false, enabled = false, pending = false, logged = false, failed = false, lab = false;
    uint32_t width = 0, height = 0;
    format pixel_format = format::unknown;
    resource image{};
    resource upload{};
    resource_view srv{};
    fence ready{};
    uint64_t ticket = 0, sequence = 0, last_seen = 0, next_capture = 0, next_connect = 0;
    sc::Frame frame;
};
std::unordered_map<effect_runtime*, std::unique_ptr<State>> states;
void info(const char* text) { reshade::log::message(reshade::log::level::info, text); }
void error(State& s, const char* text) {
    if (!s.failed) reshade::log::message(reshade::log::level::error, text);
    s.failed = true; s.enabled = false;
}
void release(effect_runtime* runtime, State& s) {
    // Only on resource resize or teardown; the capture loop never waits for GPU readback.
    if (s.image.handle || s.upload.handle || s.ready.handle) runtime->get_command_queue()->wait_idle();
    auto* device = runtime->get_device();
    if (s.srv.handle) {
        runtime->update_texture_bindings("STREETCHEM_LAB", {}, {});
        device->destroy_resource_view(s.srv);
    }
    if (s.image.handle) device->destroy_resource(s.image);
    if (s.upload.handle) device->destroy_resource(s.upload);
    if (s.ready.handle) device->destroy_fence(s.ready);
    s.image = {}; s.upload = {}; s.srv = {}; s.ready = {}; s.pending = false; s.ticket = 0;
}
void init(effect_runtime* runtime) {
    auto s = std::make_unique<State>();
    s->enabled = true;
    if (guest) s->connected = s->bus.open(true);
    states.emplace(runtime, std::move(s));
    info(guest ? "StreetChem: Schedule I diagnostic producer ready."
               : "StreetChem: Cyberpunk world consumer ready. Ctrl+Alt+L toggles diagnostic view.");
}
void destroy(effect_runtime* runtime) {
    const auto it = states.find(runtime);
    if (it != states.end()) { release(runtime, *it->second); states.erase(it); }
}
bool supported(format f) {
    return f == format::r8g8b8a8_unorm || f == format::r8g8b8a8_unorm_srgb
        || f == format::b8g8r8a8_unorm || f == format::b8g8r8a8_unorm_srgb;
}
void guest_present(effect_runtime* runtime) {
    const auto it = states.find(runtime);
    if (it == states.end()) return;
    auto& s = *it->second;
    if (runtime->is_key_down(VK_CONTROL) && runtime->is_key_down(VK_MENU) && runtime->is_key_pressed('L')) {
        s.enabled = !s.enabled; s.failed = false;
        info(s.enabled ? "StreetChem: capture enabled" : "StreetChem: capture disabled");
    }
    if (!s.connected || s.failed) return;
    auto* device = runtime->get_device();
    if (s.pending && device->get_completed_fence_value(s.ready) >= s.ticket) {
        subresource_data data{};
        if (device->map_texture_region(s.image, 0, nullptr, map_access::read_only, &data)) {
            const bool bgra = s.pixel_format == format::b8g8r8a8_unorm
                || s.pixel_format == format::b8g8r8a8_unorm_srgb;
            const bool sent = s.bus.publish(static_cast<uint8_t*>(data.data), s.width,
                s.height, data.row_pitch, bgra);
            device->unmap_texture_region(s.image, 0);
            if (sent && !s.logged) { info("StreetChem: first real Schedule I frame published"); s.logged = true; }
        }
        s.pending = false;
    }
    const auto now = GetTickCount64();
    if (!s.enabled || s.pending || now < s.next_capture) return;
    s.next_capture = now + 33;
    const auto back_buffer = runtime->get_current_back_buffer();
    const auto desc = device->get_resource_desc(back_buffer);
    if (!supported(desc.texture.format) || desc.texture.samples != 1
        || desc.texture.width > sc::max_width || desc.texture.height > sc::max_height) {
        error(s, "StreetChem: unsupported back buffer. Requires RGBA8/BGRA8, single sample, <=3840x2160.");
        return;
    }
    if (!s.image.handle || s.width != desc.texture.width || s.height != desc.texture.height
        || s.pixel_format != desc.texture.format) {
        release(runtime, s);
        s.width = desc.texture.width; s.height = desc.texture.height;
        s.pixel_format = desc.texture.format;
        const resource_desc readback(s.width, s.height, 1, 1, s.pixel_format, 1,
            memory_heap::readback, resource_usage::copy_dest);
        if (!device->create_resource(readback, nullptr, resource_usage::copy_dest, &s.image)
            || !device->create_fence(0, fence_flags::none, &s.ready)) {
            error(s, "StreetChem: GPU readback resource/fence creation failed"); return;
        }
    }
    auto* queue = runtime->get_command_queue();
    auto* commands = queue->get_immediate_command_list();
    if (!commands) { error(s, "StreetChem: no graphics command list"); return; }
    commands->barrier(back_buffer, resource_usage::present, resource_usage::copy_source);
    commands->copy_texture_region(back_buffer, 0, nullptr, s.image, 0, nullptr);
    commands->barrier(back_buffer, resource_usage::copy_source, resource_usage::present);
    queue->flush_immediate_command_list();
    ++s.ticket;
    if (!queue->signal(s.ready, s.ticket)) { error(s, "StreetChem: GPU fence signal failed"); return; }
    s.pending = true;
}
void host_effects(effect_runtime* runtime, command_list*, resource_view, resource_view) {
    const auto it = states.find(runtime);
    if (it == states.end()) return;
    auto& s = *it->second;
    if (runtime->is_key_down(VK_CONTROL) && runtime->is_key_down(VK_MENU) && runtime->is_key_pressed('L')) {
        s.lab = !s.lab; s.failed = false;
        release(runtime,s);s.bus.close();s.connected=false;s.sequence=0;s.last_seen=0;
        info(s.lab ? "StreetChem: full-frame diagnostic enabled" : "StreetChem: world visual gate enabled");
    }
    const auto now = GetTickCount64();
    if (s.enabled && !s.connected && now >= s.next_connect) {
        s.next_connect = now + 330; s.connected = s.bus.open(false,s.lab ? sc::bus_name : L"Local\\StreetChem.World.v1");
    }
    if (s.pending && runtime->get_device()->get_completed_fence_value(s.ready) >= s.ticket) s.pending = false;
    if (s.enabled && s.connected && !s.failed && !s.pending && s.bus.read(s.frame, s.sequence)) {
        auto* device = runtime->get_device();
        const auto w = s.frame.header.width, h = s.frame.header.height;
        if (!s.image.handle || s.width != w || s.height != h) {
            release(runtime, s); s.width = w; s.height = h;
            const resource_desc texture(w, h, 1, 1, format::r8g8b8a8_unorm, 1,
                memory_heap::default_, resource_usage::shader_resource | resource_usage::copy_dest);
            const uint32_t pitch = (w * 4 + 255) & ~255u;
            const resource_desc upload_desc(uint64_t(pitch) * h, memory_heap::upload, resource_usage::copy_source);
            if (!device->create_resource(texture, nullptr, resource_usage::shader_resource, &s.image)
                || !device->create_resource_view(s.image, resource_usage::shader_resource,
                    resource_view_desc(format::r8g8b8a8_unorm), &s.srv)
                || !device->create_resource(upload_desc, nullptr, resource_usage::copy_source, &s.upload)
                || !device->create_fence(0, fence_flags::none, &s.ready)) {
                error(s, "StreetChem: consumer texture creation failed"); return;
            }
        }
        if (!device->check_capability(device_caps::copy_buffer_to_texture)) {
            error(s, "StreetChem: consumer GPU cannot copy an upload buffer to texture"); return;
        }
        const uint32_t pitch = (w * 4 + 255) & ~255u;
        void* mapped = nullptr;
        if (!device->map_buffer_region(s.upload, 0, uint64_t(pitch) * h, map_access::write_only, &mapped)) {
            error(s, "StreetChem: upload buffer mapping failed"); return;
        }
        for (uint32_t row = 0; row < h; ++row) {
            std::memcpy(static_cast<uint8_t*>(mapped) + size_t(row) * pitch,
                s.frame.rgba.data() + size_t(row) * w * 4, size_t(w) * 4);
        }
        device->unmap_buffer_region(s.upload);
        auto* queue = runtime->get_command_queue();
        auto* commands = queue->get_immediate_command_list();
        if (!commands) { error(s, "StreetChem: consumer graphics command list unavailable"); return; }
        commands->barrier(s.image, resource_usage::shader_resource, resource_usage::copy_dest);
        commands->copy_buffer_to_texture(s.upload, 0, pitch / 4, h, s.image, 0, nullptr);
        commands->barrier(s.image, resource_usage::copy_dest, resource_usage::shader_resource);
        queue->flush_immediate_command_list();
        ++s.ticket;
        if (!queue->signal(s.ready, s.ticket)) { error(s, "StreetChem: consumer GPU fence failed"); return; }
        s.pending = true;
        s.sequence = s.frame.header.sequence; s.last_seen = s.frame.header.timestamp_ms;
        if (!s.logged) { info("StreetChem: first Schedule I frame uploaded in Cyberpunk"); s.logged = true; }
    }
    // Lookup every frame so effect reloads cannot leave invalid uniform handles behind.
    runtime->update_texture_bindings("STREETCHEM_LAB", s.srv, s.srv);
    s.input.write(runtime, s.lab && s.enabled && !s.failed && s.srv.handle && now >= s.last_seen && now - s.last_seen < 1500);
    const auto world = runtime->find_uniform_variable(effect, "SCWorldVisual");
    if(world.handle)runtime->set_uniform_value_bool(world,!s.lab);
    const auto show = runtime->find_uniform_variable(effect, "SCLabVisible");
    if (show.handle) runtime->set_uniform_value_bool(show,
        s.enabled && !s.failed && s.srv.handle && now >= s.last_seen && now - s.last_seen < 1500);
    const auto aspect = runtime->find_uniform_variable(effect, "SCLabAspect");
    if (aspect.handle && s.height) runtime->set_uniform_value_float(aspect, float(s.width) / float(s.height));
}
}
extern "C" __declspec(dllexport) const char* NAME = "Street Chem Render Bridge";
extern "C" __declspec(dllexport) const char* DESCRIPTION = "Live Schedule I laboratory render transport. Gameplay integration in development.";
BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        wchar_t path[MAX_PATH]; GetModuleFileNameW(nullptr, path, MAX_PATH);
        const wchar_t* name = wcsrchr(path, L'\\'); name = name ? name + 1 : path;
        guest = _wcsicmp(name, L"Schedule I.exe") == 0;
        if (!guest && _wcsicmp(name, L"Cyberpunk2077.exe") != 0) return FALSE;
        if (!reshade::register_addon(module)) return FALSE;
        reshade::register_event<reshade::addon_event::init_effect_runtime>(init);
        reshade::register_event<reshade::addon_event::destroy_effect_runtime>(destroy);
        if (guest) reshade::register_event<reshade::addon_event::reshade_present>(guest_present);
        else reshade::register_event<reshade::addon_event::reshade_begin_effects>(host_effects);
    } else if (reason == DLL_PROCESS_DETACH) reshade::unregister_addon(module);
    return TRUE;
}
