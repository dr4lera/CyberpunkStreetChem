// Melty's test mode (C++, a DLL the game loads). While Melty tests a release it writes %LOCALAPPDATA%\Melty\test\first-experimental-solo-releas.json; its "checks" names the file to append one line per check to, verdict first: "pass <name>" or "fail <name>: why". Report every name in recipe test.checks (and list only names you report). Never quit the game in a test run: Melty closes it when the run's minutes are up. Without the file (a player's game), nothing happens. Call MeltyTestStart() from your plugin's load (outside DllMain if you can), then MeltyTestReport(...) as each check happens.
#include <windows.h>
#include <fstream>
#include <sstream>
#include <string>

static std::wstring g_meltyChecks;

static std::wstring MeltyWide(const std::string& utf8) {
    int n = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), (int)utf8.size(), nullptr, 0);
    std::wstring wide(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), (int)utf8.size(), &wide[0], n);
    return wide;
}

void MeltyTestReport(const char* name, bool passed, const char* why = nullptr) {
    if (g_meltyChecks.empty()) return;
    std::ofstream out(g_meltyChecks, std::ios::app | std::ios::binary);
    out << (passed ? "pass " : "fail ") << name;
    if (!passed && why) out << ": " << why;
    out << "\n";
}

void MeltyTestStart() {
    wchar_t local[MAX_PATH];
    if (!GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH)) return;
    std::ifstream in(std::wstring(local) + L"\\Melty\\test\\first-experimental-solo-releas.json", std::ios::binary);
    if (!in) return; // not a test run
    std::stringstream text; text << in.rdbuf();
    const std::string json = text.str();
    size_t at = json.find("\"checks\"");
    if (at == std::string::npos) return;
    at = json.find('"', json.find(':', at + 8));
    if (at == std::string::npos) return;
    std::string path;
    for (size_t i = at + 1; i < json.size() && json[i] != '"'; ++i) {
        if (json[i] == '\\' && i + 1 < json.size()) ++i; // JSON writes each backslash twice
        path += json[i];
    }
    g_meltyChecks = MeltyWide(path);
    MeltyTestReport("mod-loads", true);
}
