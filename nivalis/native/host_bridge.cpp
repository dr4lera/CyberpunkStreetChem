#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <winhttp.h>
#include <objbase.h>
#include <RED4ext/RED4ext.hpp>
#include <RED4ext/CString.hpp>
#include <json.hpp>
#include <filesystem>
#include <fstream>
#include <thread>
#include <mutex>
#include <deque>
#include <atomic>
#include <unordered_map>
#include <array>

using Json = nlohmann::json;
using Context = RED4ext::IScriptable;
using Frame = RED4ext::CStackFrame;
namespace {
HINSTANCE moduleHandle;
std::atomic_bool running = false;
std::thread worker;
std::thread keyWorker;
std::mutex guard;
std::deque<Json> queue;
Json snapshot = Json::object(), status = Json::object(), shopping = Json::object(), hostRuntime = Json::object();
std::unordered_map<std::string, Json> replies;
std::unordered_map<std::string, uint64_t> nextReceiptRead;
uint64_t lastSuccess = 0;
std::filesystem::path game, evidence, configPath;
std::filesystem::path transactionRoot;
std::string labSave;
std::string lastError;
bool autoRestock = false;
int developerManagerTravel = -1;
std::array<std::atomic_bool, 256> pressed{};
std::array<bool, 256> previous{};
std::array<std::atomic_int, 256> modifiers{};
bool ValidId(const std::string& id) {
    return id.size() == 36 && id.find_first_not_of("0123456789abcdefABCDEF-") == std::string::npos;
}
bool Focused() { DWORD pid = 0; GetWindowThreadProcessId(GetForegroundWindow(), &pid); return pid == GetCurrentProcessId(); }
bool WriteTicket(const std::string& id, const Json& data);

struct HttpHandle {
    HINTERNET value;
    ~HttpHandle() { if (value) WinHttpCloseHandle(value); }
};
std::string Escape(const std::string& input) {
    const char* hex = "0123456789ABCDEF";
    std::string out;
    for (unsigned char c : input) {
        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.') out += c;
        else { out += '%'; out += hex[c >> 4]; out += hex[c & 15]; }
    }
    return out;
}
Json Exchange(const std::string& op, const Json& args) {
    std::ifstream tokenFile(game / L"BepInEx/cache/nivalismodkit-bridge.token");
    std::string token; std::getline(tokenFile, token);
    if (!token.empty() && token.back() == '\r') token.pop_back();
    if (token.empty()) throw std::runtime_error("guest token unavailable");
    std::string path = "/cmd/" + Escape(op) + "?";
    for (const auto& [key, value] : args.items()) path += Escape(key) + "=" + Escape(value.is_string() ? value.get<std::string>() : value.dump()) + "&";
    std::wstring widePath(path.begin(), path.end()), headers = L"X-Kit-Token: " + std::wstring(token.begin(), token.end()) + L"\r\n";
    HttpHandle session{WinHttpOpen(L"NivalisNightCity/0.1", WINHTTP_ACCESS_TYPE_NO_PROXY, nullptr, nullptr, 0)};
    if (!session.value) throw std::runtime_error("HTTP session failed");
    WinHttpSetTimeouts(session.value, 1500, 1500, 1500, 4000);
    HttpHandle connection{WinHttpConnect(session.value, L"127.0.0.1", 5710, 0)};
    HttpHandle request{WinHttpOpenRequest(connection.value, L"POST", widePath.c_str(), nullptr, nullptr, WINHTTP_DEFAULT_ACCEPT_TYPES, 0)};
    if (!request.value || !WinHttpSendRequest(request.value, headers.c_str(), static_cast<DWORD>(headers.size()), nullptr, 0, 0, 0) || !WinHttpReceiveResponse(request.value, nullptr))
        throw std::runtime_error("guest unavailable");
    DWORD code = 0, codeSize = sizeof(code);
    WinHttpQueryHeaders(request.value, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, nullptr, &code, &codeSize, nullptr);
    std::string body;
    char buffer[8192]; DWORD read = 0;
    while (WinHttpReadData(request.value, buffer, sizeof(buffer), &read) && read) {
        body.append(buffer, read);
        if (body.size() > 4 * 1024 * 1024) throw std::runtime_error("guest response too large");
    }
    Json result = Json::parse(body);
    if (code != 200) throw std::runtime_error(result.value("error", std::string("guest command failed")));
    return result;
}
void Report() {
    if (evidence.empty()) return;
    try {
        Json report{{"connected", lastSuccess && GetTickCount64() - lastSuccess < 5000}, {"error", lastError}, {"status", status}, {"snapshot", snapshot},
            {"leaseReply", replies.contains("lease") ? replies["lease"] : Json::object()}, {"host", hostRuntime}};
        auto temp = evidence; temp += L".tmp";
        std::ofstream file(temp); file << report.dump(2); file.close();
        MoveFileExW(temp.c_str(), evidence.c_str(), MOVEFILE_REPLACE_EXISTING);
    } catch (...) { }
}
void Run() {
    uint64_t nextSnapshot = 0, nextShopping = 0;
    while (running) {
        if (labSave.empty() || game.empty()) { Sleep(250); continue; }
        Json command;
        { std::lock_guard lock(guard); if (!queue.empty()) { command = queue.front(); queue.pop_front(); } }
        const auto now = GetTickCount64();
        if (command.is_null() && now >= nextShopping && status.value("ready", false)) {
            command = {{"op", "nc-shopping-list"}, {"id", "shopping"}, {"args", Json::object()}};
            nextShopping = now + 3000;
        } else if (command.is_null() && now >= nextSnapshot) {
            command = {{"op", "nc-venues"}, {"id", "observe"}, {"args", Json::object()}};
            nextSnapshot = now + 2000;
        }
        if (!command.is_null()) {
            try {
                auto result = Exchange(command["op"], command["args"]);
                std::lock_guard lock(guard);
                lastSuccess = GetTickCount64();
                lastError.clear();
                if (command["id"] == "observe") {
                    snapshot = result; status = result.value("status", Json::object());
                    Json owned = Json::array();
                    for (const auto& venue : snapshot.value("venues", Json::array()))
                        if (venue.value("owned", false)) owned.push_back(venue);
                    snapshot["venues"] = std::move(owned);
                }
                else if (command["id"] == "shopping") shopping = result;
                else {
                    result["ok"] = true;
                    replies[command["id"].get<std::string>()] = result;
                    if (command["op"] == "nc-buy-shopping-list" || command["op"] == "nc-restock-receipt" ||
                        command["op"] == "nc-cash-settle" || command["op"] == "nc-cash-receipt") {
                        const auto id = command["id"].get<std::string>();
                        Json ticket; std::ifstream file(transactionRoot / (id + ".json")); file >> ticket;
                        file.close();
                        const auto ticketState = ticket.value("status", std::string());
                        if (ticketState != "settled" && ticketState != "cancelled_refunded" &&
                            !ticket.value("refundApprovedAfterGuestRestore", false)) {
                            ticket["receipt"] = result;
                            ticket["status"] = result.value("Status", std::string("needs_recovery"));
                            if (!WriteTicket(id, ticket)) throw std::runtime_error("receipt journal write failed");
                        }
                        if ((command["op"] == "nc-buy-shopping-list" || command["op"] == "nc-restock-receipt") &&
                            result.value("Status", std::string()) == "committed") {
                            shopping = Json::object(); nextShopping = 0;
                        }
                    }
                    if (replies.size() > 256) replies.clear();
                }
                Report();
            } catch (const std::exception& e) {
                std::lock_guard lock(guard);
                replies[command["id"].get<std::string>()] = {{"ok", false}, {"error", e.what()}};
                lastError = e.what();
                if (replies.size() > 256) replies.clear();
                Report();
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
}
void RunKeys() {
    while (running) {
        const bool focused = Focused();
        const int mask = ((GetAsyncKeyState(VK_CONTROL) & 0x8000) ? 1 : 0) |
            ((GetAsyncKeyState(VK_SHIFT) & 0x8000) ? 2 : 0) | ((GetAsyncKeyState(VK_MENU) & 0x8000) ? 4 : 0);
        for (int key = 1; key < 256; ++key) {
            const bool down = focused && (GetAsyncKeyState(key) & 0x8000);
            if (down && !previous[key]) { modifiers[key] = mask; pressed[key] = true; }
            previous[key] = down;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
}
void Configure() {
    wchar_t path[32768]; GetModuleFileNameW(moduleHandle, path, 32768);
    const auto root = std::filesystem::path(path).parent_path();
    configPath = root / L"NivalisNightCity.json";
    evidence = root / L"host-status.json";
    transactionRoot = root / L"transactions";
    std::filesystem::create_directories(transactionRoot);
    std::ifstream file(configPath);
    Json cfg; file >> cfg;
    const auto utf8 = cfg.at("nivalisGame").get<std::string>();
    game = std::filesystem::path(std::u8string(reinterpret_cast<const char8_t*>(utf8.data()), utf8.size()));
    labSave = cfg.at("save").get<std::string>();
    evidence = root / L"host-status.json";
    transactionRoot = root / L"transactions";
    std::filesystem::create_directories(transactionRoot);
    autoRestock = cfg.value("autoRestock", false);
    developerManagerTravel = cfg.value("developerManagerTravel", -1);
}
void Connected(Context*, Frame* f, bool* out, int64_t) {
    f->code++; std::lock_guard lock(guard);
    if (out) *out = lastSuccess && GetTickCount64() - lastSuccess < 5000 && status.value("ready", false) && status.value("save", std::string()) == labSave;
}
void Clock(Context*, Frame* f, float* out, int64_t) { f->code++; if (out) *out = static_cast<float>(GetTickCount64() / 1000.0); }
void DeveloperTravel(Context*, Frame* f, int32_t* out, int64_t) {
    f->code++; std::lock_guard lock(guard); if (out) *out = developerManagerTravel; developerManagerTravel = -1;
}
void ReplyState(Context*, Frame* f, RED4ext::CString* out, int64_t) {
    RED4ext::CString id; RED4ext::GetParameter(f, &id); f->code++;
    std::lock_guard lock(guard); std::string result = "pending";
    if (replies.contains(id.c_str())) result = replies[id.c_str()].value("ok", false) ? "done" : "failed";
    if (out) *out = RED4ext::CString(result);
}
void NewId(Context*, Frame* f, RED4ext::CString* out, int64_t) {
    f->code++; GUID guid{}; CoCreateGuid(&guid); wchar_t wide[40]; StringFromGUID2(guid, wide, 40);
    std::string id; for (int i = 1; i < 37; ++i) id += static_cast<char>(wide[i]);
    if (out) *out = RED4ext::CString(id);
}
void Lease(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString pair; RED4ext::GetParameter(f, &pair); f->code++;
    std::lock_guard lock(guard); if (out) *out = false;
    if (queue.size() >= 16) return;
    queue.push_back({{"op", "nc-lease"}, {"id", "lease"}, {"args", {{"save", labSave}, {"pair", pair.c_str()}, {"seconds", 5}}}});
    if (out) *out = true;
}
void Count(Context*, Frame* f, int32_t* out, int64_t) {
    f->code++; std::lock_guard lock(guard); if (out) *out = snapshot.contains("venues") ? static_cast<int32_t>(snapshot["venues"].size()) : 0;
}
void Day(Context*, Frame* f, int32_t* out, int64_t) { f->code++; std::lock_guard lock(guard); if (out) *out = status.value("day", -1); }
void RestockedDay(Context*, Frame* f, int32_t* out, int64_t) { f->code++; std::lock_guard lock(guard); if (out) *out = status.value("lastRestockDay", -1); }
void CashDay(Context*, Frame* f, int32_t* out, int64_t) { f->code++; std::lock_guard lock(guard); if (out) *out = status.value("lastCashDay", -1); }
void CashNet(Context*, Frame* f, int32_t* out, int64_t) {
    f->code++; std::lock_guard lock(guard);
    if (out) *out = status.value("maintenance", true) || status.value("paperTest", false) || !status.value("leaseActive", false) ? 0 : status.value("cashNetEddies", 0);
}
void AutoRestock(Context*, Frame* f, bool* out, int64_t) { f->code++; if (out) *out = autoRestock; }
void ToggleRestock(Context*, Frame* f, bool* out, int64_t) {
    f->code++;
    try {
        Json cfg; std::ifstream file(configPath); file >> cfg; file.close();
        cfg["autoRestock"] = !autoRestock;
        auto temp = configPath; temp += L".tmp";
        std::ofstream target(temp); target << cfg.dump(2); target.close();
        if (MoveFileExW(temp.c_str(), configPath.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) autoRestock = !autoRestock;
    } catch (...) { }
    if (out) *out = autoRestock;
}
void Hotkey(Context*, Frame* f, bool* out, int64_t) {
    int32_t key = 0, mask = 0; RED4ext::GetParameter(f, &key); RED4ext::GetParameter(f, &mask); f->code++;
    if (out) *out = key > 0 && key < 256 && Focused() && modifiers[key] == mask && pressed[key].exchange(false);
}
void HostReport(Context*, Frame* f, void*, int64_t) {
    RED4ext::CString payload; RED4ext::GetParameter(f, &payload); f->code++;
    try { if (strlen(payload.c_str()) > 8192) return; auto data = Json::parse(payload.c_str());
        if (!data.is_object()) return; std::lock_guard lock(guard); hostRuntime = std::move(data); } catch (...) { }
}
void Request(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString op, id, payload; RED4ext::GetParameter(f, &op); RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &payload); f->code++;
    if (out) *out = false;
    try {
        const std::string operation = op.c_str();
        if (operation != "nc-menu" && operation != "nc-maintenance" && operation != "nc-save" && operation != "nc-manager-action") return;
        if (!ValidId(id.c_str()) || strlen(payload.c_str()) > 8192) return;
        auto args = Json::parse(payload.c_str()); if (!args.is_object()) return;
        args["save"] = labSave;
        std::lock_guard lock(guard); if (queue.size() >= 16) return;
        if (operation == "nc-manager-action") {
            if (status.value("paperTest", false)) return;
            for (const auto& entry : std::filesystem::directory_iterator(transactionRoot)) {
                if (entry.path().extension() != ".json") continue;
                Json ticket; std::ifstream file(entry.path()); file >> ticket;
                const auto state = ticket.value("status", std::string());
                if (state != "settled" && state != "cancelled_refunded") return;
            }
        }
        queue.push_back({{"op", operation}, {"id", id.c_str()}, {"args", args}});
        if (out) *out = true;
    } catch (...) { }
}
void QuoteCost(Context*, Frame* f, int32_t* out, int64_t) {
    f->code++; std::lock_guard lock(guard);
    if (out) *out = 0;
    if (status.value("maintenance", true) || status.value("paperTest", false) || status.value("purchaseInProgress", false) ||
        !status.contains("pair") || !status["pair"].is_string() || !status.value("leaseActive", false)) return;
    const auto cost = shopping.value("reserveEddies", int64_t(0));
    if (out) *out = shopping.value("day", -2) == status.value("day", -1) && cost <= 20000000 ? static_cast<int32_t>(cost) : 0;
}
bool WriteTicket(const std::string& id, const Json& data) {
    if (!ValidId(id)) return false;
    const auto path = transactionRoot / (id + ".json"); auto temp = path; temp += L".tmp";
    std::ofstream file(temp); file << data.dump(2); file.close();
    return MoveFileExW(temp.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
}
void BeginRestock(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString pair, id; int32_t budget = 0, before = 0, after = 0;
    RED4ext::GetParameter(f, &pair); RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &budget);
    RED4ext::GetParameter(f, &before); RED4ext::GetParameter(f, &after); f->code++;
    if (out) *out = false;
    if (!ValidId(id.c_str()) || !ValidId(pair.c_str()) || budget <= 0 || budget > 20000000 || static_cast<int64_t>(before) - after != budget) return;
    std::lock_guard lock(guard); if (queue.size() >= 16) return;
    try {
        if (status.value("maintenance", true) || status.value("paperTest", false) || !status.contains("pair") ||
            !status["pair"].is_string() || status["pair"].get<std::string>() != pair.c_str()) return;
        auto path = transactionRoot / (std::string(id.c_str()) + ".json");
        if (std::filesystem::exists(path)) return;
        if (!WriteTicket(id.c_str(), {{"id", id.c_str()}, {"pair", pair.c_str()}, {"budget", budget},
            {"before", before}, {"after", after}, {"status", "reserved"}, {"day", status.value("day", -1)}})) return;
        queue.push_back({{"op", "nc-buy-shopping-list"}, {"id", id.c_str()},
            {"args", {{"save", labSave}, {"pair", pair.c_str()}, {"id", id.c_str()}, {"budget", budget}}}});
        if (out) *out = true;
    } catch (...) { }
}
void BeginCash(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString pair, id; int32_t budget = 0, before = 0, after = 0;
    RED4ext::GetParameter(f, &pair); RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &budget);
    RED4ext::GetParameter(f, &before); RED4ext::GetParameter(f, &after); f->code++;
    if (out) *out = false;
    if (!ValidId(id.c_str()) || !ValidId(pair.c_str()) || budget < 0 || budget > 20000000 || static_cast<int64_t>(before) - after != budget) return;
    std::lock_guard lock(guard); if (queue.size() >= 16) return;
    try {
        if (status.value("maintenance", true) || status.value("paperTest", false) || !status.value("leaseActive", false) ||
            !status.contains("pair") || !status["pair"].is_string() || status["pair"].get<std::string>() != pair.c_str()) return;
        if (std::filesystem::exists(transactionRoot / (std::string(id.c_str()) + ".json"))) return;
        if (!WriteTicket(id.c_str(), {{"id", id.c_str()}, {"kind", "cash"}, {"pair", pair.c_str()}, {"budget", budget},
            {"before", before}, {"after", after}, {"status", "reserved"}, {"day", status.value("day", -1)}})) return;
        queue.push_back({{"op", "nc-cash-settle"}, {"id", id.c_str()},
            {"args", {{"save", labSave}, {"pair", pair.c_str()}, {"id", id.c_str()}, {"budget", budget}}}});
        if (out) *out = true;
    } catch (...) { }
}
void RestockResult(Context*, Frame* f, RED4ext::CString* out, int64_t) {
    RED4ext::CString id; RED4ext::GetParameter(f, &id); f->code++; std::lock_guard lock(guard);
    std::string state = "pending";
    if (!ValidId(id.c_str())) { if (out) *out = RED4ext::CString("needs_recovery"); return; }
    try {
        Json ticket; std::ifstream file(transactionRoot / (std::string(id.c_str()) + ".json")); file >> ticket;
        state = ticket.value("status", std::string("needs_recovery"));
        if (state == "settled") state = "needs_recovery"; // Host save rollback: never refund twice.
        else if (ticket.contains("receipt")) replies[id.c_str()] = ticket["receipt"];
        if ((state == "reserved" || state == "pending") && GetTickCount64() >= nextReceiptRead[id.c_str()] && queue.size() < 16) {
            nextReceiptRead[id.c_str()] = GetTickCount64() + 1500;
            replies[id.c_str()] = {{"Status", "pending"}};
            queue.push_back({{"op", ticket.value("kind", std::string()) == "cash" ? "nc-cash-receipt" : "nc-restock-receipt"},
                {"id", id.c_str()}, {"args", {{"id", id.c_str()}}}});
        }
        if (replies.contains(id.c_str()) && !replies[id.c_str()].value("ok", true)) state = "needs_recovery";
    } catch (...) { state = "needs_recovery";
    }
    if (out) *out = RED4ext::CString(state);
}
void ResultInt(Context*, Frame* f, int32_t* out, int64_t) {
    RED4ext::CString id, field; RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &field); f->code++;
    std::lock_guard lock(guard); if (out) *out = replies.contains(id.c_str()) ? replies[id.c_str()].value(field.c_str(), int32_t(0)) : 0;
}
int ApprovedRefund(const Json& ticket, const char* pair, int budget) {
    const int reserved = ticket.value("budget", -1);
    const int amount = ticket.value("refundEddiesAfterGuestRestore", reserved);
    // Older host scripts overwrote the completed reservation budget with a zero
    // shopping quote. A settled-ticket restore still has its audited receipt.
    const bool recoveredBudget = budget == 0 && ticket.value("preRestoreStatus", std::string()) == "settled" &&
        ticket.contains("receipt") && ticket["receipt"].value("BudgetEddies", -2) == reserved &&
        ticket["receipt"].value("ChargeEddies", -2) == amount &&
        amount + ticket["receipt"].value("RefundEddies", -2) == reserved;
    return ticket.value("refundApprovedAfterGuestRestore", false) && ticket.value("status", std::string()) == "reserved" &&
        ticket.value("pair", std::string()) == pair && (reserved == budget || recoveredBudget) && amount > 0 && amount <= reserved ? amount : 0;
}
void RefundAmount(Context*, Frame* f, int32_t* out, int64_t) {
    RED4ext::CString id, pair; int32_t budget = 0;
    RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &pair); RED4ext::GetParameter(f, &budget); f->code++;
    if (out) *out = 0;
    if (!ValidId(id.c_str()) || !ValidId(pair.c_str())) return;
    std::lock_guard lock(guard);
    try { Json ticket; std::ifstream file(transactionRoot / (std::string(id.c_str()) + ".json")); file >> ticket;
        if (out) *out = ApprovedRefund(ticket, pair.c_str(), budget);
    } catch (...) { }
}
void RefundReady(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString id, pair; int32_t budget = 0;
    RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &pair); RED4ext::GetParameter(f, &budget); f->code++;
    if (out) *out = false;
    if (!ValidId(id.c_str()) || !ValidId(pair.c_str())) return;
    std::lock_guard lock(guard);
    try { Json ticket; std::ifstream file(transactionRoot / (std::string(id.c_str()) + ".json")); file >> ticket;
        if (out) *out = ApprovedRefund(ticket, pair.c_str(), budget) > 0;
    } catch (...) { }
}
void FinishRefund(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString id; int32_t balance = 0;
    RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &balance); f->code++;
    if (out) *out = false; if (!ValidId(id.c_str())) return;
    std::lock_guard lock(guard);
    try { Json ticket; std::ifstream file(transactionRoot / (std::string(id.c_str()) + ".json")); file >> ticket;
        file.close();
        if (!ticket.value("refundApprovedAfterGuestRestore", false) || ticket.value("status", std::string()) != "reserved") return;
        ticket["status"] = "cancelled_refunded"; ticket["refundApprovedAfterGuestRestore"] = false; ticket["refundedBalance"] = balance;
        ticket["refundedEddies"] = ticket.value("refundEddiesAfterGuestRestore", ticket.value("budget", 0));
        if (out) *out = WriteTicket(id.c_str(), ticket);
    } catch (...) { }
}
void Settle(Context*, Frame* f, bool* out, int64_t) {
    RED4ext::CString id; int32_t balance = 0; RED4ext::GetParameter(f, &id); RED4ext::GetParameter(f, &balance); f->code++;
    if (out) *out = false; if (!ValidId(id.c_str())) return; std::lock_guard lock(guard);
    try {
        Json ticket; std::ifstream file(transactionRoot / (std::string(id.c_str()) + ".json")); file >> ticket;
        file.close();
        if (!replies.contains(id.c_str()) || replies[id.c_str()].value("Status", std::string()) != "committed") return;
        ticket["status"] = "settled"; ticket["receipt"] = replies[id.c_str()]; ticket["settledBalance"] = balance;
        if (out) *out = WriteTicket(id.c_str(), ticket);
    } catch (...) { }
}
void Value(Context*, Frame* f, RED4ext::CString* out, int64_t) {
    int32_t index = 0; RED4ext::CString field;
    RED4ext::GetParameter(f, &index); RED4ext::GetParameter(f, &field); f->code++;
    std::lock_guard lock(guard); std::string value;
    if (snapshot.contains("venues") && index >= 0 && index < snapshot["venues"].size()) {
        const auto row = snapshot["venues"][index];
        if (row.contains(field.c_str())) { const auto v = row[field.c_str()]; value = v.is_string() ? v.get<std::string>() : v.dump(); }
    }
    if (out) *out = RED4ext::CString(value);
}
void MenuValue(Context*, Frame* f, RED4ext::CString* out, int64_t) {
    int32_t index = 0, dish = 0; RED4ext::CString field;
    RED4ext::GetParameter(f, &index); RED4ext::GetParameter(f, &dish); RED4ext::GetParameter(f, &field); f->code++;
    std::lock_guard lock(guard); std::string value;
    if (snapshot.contains("venues") && index >= 0 && index < snapshot["venues"].size()) {
        auto menu = snapshot["venues"][index].value("menu", Json::array());
        if (dish >= 0 && dish < menu.size() && menu[dish].contains(field.c_str())) {
            auto v = menu[dish][field.c_str()]; value = v.is_string() ? v.get<std::string>() : v.dump();
        }
    }
    if (out) *out = RED4ext::CString(value);
}
void Register() {
    auto rtti = RED4ext::CRTTISystem::Get();
    auto add = [&](const char* name, auto callback, const char* returns, std::initializer_list<std::pair<const char*, const char*>> params = {}) {
        auto fn = RED4ext::CGlobalFunction::Create(name, name, callback); fn->flags = {.isNative = true, .isStatic = true}; fn->SetReturnType(returns);
        for (auto p : params) fn->AddParam(p.first, p.second); rtti->RegisterFunction(fn);
    };
    add("NC_Connected", Connected, "Bool"); add("NC_NewID", NewId, "String");
    add("NC_Clock", Clock, "Float"); add("NC_ReplyState", ReplyState, "String", {{"String", "id"}});
    add("NC_DeveloperManagerTravel", DeveloperTravel, "Int32");
    add("NC_Lease", Lease, "Bool", {{"String", "pair"}}); add("NC_Count", Count, "Int32");
    add("NC_VenueValue", Value, "String", {{"Int32", "index"}, {"String", "field"}});
    add("NC_MenuValue", MenuValue, "String", {{"Int32", "index"}, {"Int32", "dish"}, {"String", "field"}});
    add("NC_CashNet", CashNet, "Int32"); add("NC_LastCashDay", CashDay, "Int32");
    add("NC_Day", Day, "Int32"); add("NC_LastRestockDay", RestockedDay, "Int32"); add("NC_AutoRestock", AutoRestock, "Bool");
    add("NC_ShoppingCost", QuoteCost, "Int32");
    add("NC_Hotkey", Hotkey, "Bool", {{"Int32", "key"}, {"Int32", "modifiers"}});
    add("NC_ToggleRestock", ToggleRestock, "Bool");
    add("NC_Request", Request, "Bool", {{"String", "op"}, {"String", "id"}, {"String", "args"}});
    add("NC_Report", HostReport, "Void", {{"String", "state"}});
    add("NC_BeginRestock", BeginRestock, "Bool", {{"String", "pair"}, {"String", "id"}, {"Int32", "budget"}, {"Int32", "before"}, {"Int32", "after"}});
    add("NC_BeginCash", BeginCash, "Bool", {{"String", "pair"}, {"String", "id"}, {"Int32", "budget"}, {"Int32", "before"}, {"Int32", "after"}});
    add("NC_RestockState", RestockResult, "String", {{"String", "id"}});
    add("NC_RestockValue", ResultInt, "Int32", {{"String", "id"}, {"String", "field"}});
    add("NC_SettleRestock", Settle, "Bool", {{"String", "id"}, {"Int32", "balance"}});
    add("NC_RefundReady", RefundReady, "Bool", {{"String", "id"}, {"String", "pair"}, {"Int32", "budget"}});
    add("NC_RefundAmount", RefundAmount, "Int32", {{"String", "id"}, {"String", "pair"}, {"Int32", "budget"}});
    add("NC_FinishRefund", FinishRefund, "Bool", {{"String", "id"}, {"Int32", "balance"}});
}
}
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) { if (reason == DLL_PROCESS_ATTACH) moduleHandle = instance; return TRUE; }
RED4EXT_C_EXPORT bool RED4EXT_CALL Main(RED4ext::v1::PluginHandle, RED4ext::v1::EMainReason reason, const RED4ext::v1::Sdk*) {
    if (reason == RED4ext::v1::EMainReason::Load) {
        try { Configure(); } catch (...) { game.clear(); labSave.clear(); }
        RED4ext::CRTTISystem::Get()->AddPostRegisterCallback(Register); running = true; worker = std::thread(Run); keyWorker = std::thread(RunKeys);
    }
    if (reason == RED4ext::v1::EMainReason::Unload) { running = false; if (worker.joinable()) worker.join(); if (keyWorker.joinable()) keyWorker.join(); }
    return true;
}
RED4EXT_C_EXPORT void RED4EXT_CALL Query(RED4ext::v1::PluginInfo* info) {
    info->name = L"Nivalis Night City"; info->author = L"dr4lera / Codex"; info->version = RED4EXT_V1_SEMVER(0, 1, 0);
    info->runtime = RED4EXT_V1_RUNTIME_VERSION_LATEST; info->sdk = RED4EXT_V1_SDK_VERSION_CURRENT;
}
RED4EXT_C_EXPORT uint32_t RED4EXT_CALL Supports() { return RED4EXT_API_VERSION_1; }
