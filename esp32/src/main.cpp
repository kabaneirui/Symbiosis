#include <Arduino.h>
#include <WiFi.h>
#include <driver/i2s.h>
#include <HTTPClient.h>
#include "config.h"
#include "api_client.h"

#if HAS_EYES
#include "display.h"
EyeDisplay leftEye, rightEye;
EyeRenderer eyes;
unsigned long lastLookChange = 0;
#endif

#if HAS_SPEAKER
#include "audio.h"
AudioPlayer speaker;
#endif

ApiClient api;

#if HAS_SPEAKER
uint8_t* base64Decode(const String& input, size_t* outLen);
#endif
String extractJson(const String& json, const String& key);

void connectWiFi() {
    Serial.print("连接 WiFi: " + String(WIFI_SSID));
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    int attempts = 0;
    while (WiFi.status() != WL_CONNECTED && attempts < 30) {
        delay(500); Serial.print(".");
        attempts++;
    }
    if (WiFi.status() == WL_CONNECTED)
        Serial.println("\nWiFi OK! IP: " + WiFi.localIP().toString());
    else
        Serial.println("\nWiFi 失败!");
}

void playNotify() {
#if HAS_SPEAKER
    speaker.begin();
    speaker.playTone(880, 80);
    speaker.stop();
#endif
}

void updateEyes() {
#if HAS_EYES
    eyes.setExpression(api.expression);
    eyes.update();
    if (millis() - lastLookChange > random(2000, 5000)) {
        lastLookChange = millis();
        eyes.lookRandom();
    }
#endif
}

// 流式聊天 — 发送消息，SSE 接收逐句文本+音频
void streamChat(const String& message) {
    Serial.println("你: " + message);

    HTTPClient http;
    api.httpBegin(http, "/chat/stream");
    http.addHeader("Content-Type", "application/json");
    http.setTimeout(60000);

    String body = "{\"user_id\":" + String(api.userId) + ",\"message\":\"" + message + "\"}";
    int code = http.POST(body);

    if (code != 200) {
        Serial.println("流式请求失败: " + String(code));
        // 回退到普通聊天
        String reply = api.chat(message);
        Serial.println("小星: " + reply);
        playNotify();
        return;
    }

    // 读取 SSE 流
    WiFiClient* stream = http.getStreamPtr();
    String line;
    Serial.print("小星: ");

    while (http.connected()) {
        if (!stream->available()) {
            delay(10);
            continue;
        }

        line = stream->readStringUntil('\n');
        if (!line.startsWith("data: ")) continue;

        String data = line.substring(6);

        if (data.indexOf("\"type\":\"text\"") >= 0 || data.indexOf("\"type\": \"text\"") >= 0) {
            String text = extractJson(data, "text");
            Serial.print(text);
        }
        else if (data.indexOf("\"type\":\"audio\"") >= 0 || data.indexOf("\"type\": \"audio\"") >= 0) {
#if HAS_SPEAKER
            String audioB64 = extractJson(data, "audio_base64");
            if (audioB64.length() > 100) {
                size_t audioLen = 0;
                uint8_t* audioData = base64Decode(audioB64, &audioLen);
                if (audioData && audioLen > 10) {
                    speaker.playMp3(audioData, audioLen);
                    free(audioData);
                }
            }
#endif
        }
        else if (data.indexOf("\"type\":\"done\"") >= 0 || data.indexOf("\"type\": \"done\"") >= 0) {
            Serial.println();
            String expr = extractJson(data, "expression");
            api.expression = expr;
            updateEyes();

            String favorStr = extractJson(data, "favorability");
            if (favorStr.length() > 0) api.favorability = favorStr.toInt();

            Serial.println("[好感:" + String(api.favorability) + " 表情:" + api.expression + "]");
            break;
        }
    }

    http.end();
}

// 轮询后端看有没有从 H5 发的新消息
void pollForH5Reply() {
    static unsigned long lastPoll = 0;
    static int lastReplyId = 0;

    if (millis() - lastPoll < 5000) return;
    lastPoll = millis();

    HTTPClient http;
    api.httpBegin(http, "/robot/poll?since_id=" + String(lastReplyId));
    http.setTimeout(10000);
    int code = http.GET();

    if (code == 200) {
        String resp = http.getString();
        if (resp.indexOf("\"has_new\": true") >= 0 || resp.indexOf("\"has_new\":true") >= 0) {
            int idIdx = resp.indexOf("\"id\":");
            int newId = 0;
            if (idIdx >= 0) {
                int ns = idIdx + 5;
                while (ns < (int)resp.length() && resp[ns] == ' ') ns++;
                newId = resp.substring(ns).toInt();
            }
            if (newId > lastReplyId) {
                lastReplyId = newId;
                String reply = extractJson(resp, "reply");
                if (reply.length() > 0) {
                    Serial.println("[H5] 小星: " + reply);

#if HAS_SPEAKER
                    // 下载音频
                    if (resp.indexOf("\"has_audio\": true") >= 0 || resp.indexOf("\"has_audio\":true") >= 0) {
                        HTTPClient audioHttp;
                        api.httpBegin(audioHttp, "/robot/audio");
                        audioHttp.setTimeout(15000);
                        int ac = audioHttp.GET();
                        if (ac == 200) {
                            int len = audioHttp.getSize();
                            if (len > 100) {
                                uint8_t* buf = (uint8_t*)ps_malloc(len);
                                if (!buf) buf = (uint8_t*)malloc(len);
                                if (buf) {
                                    WiFiClient* s = audioHttp.getStreamPtr();
                                    int dl = 0;
                                    while (dl < len) {
                                        int av = s->available();
                                        if (av > 0) { s->readBytes(buf + dl, min(av, len - dl)); dl += min(av, len - dl); }
                                        delay(1);
                                    }
                                    speaker.playMp3(buf, len);
                                    free(buf);
                                }
                            }
                        }
                        audioHttp.end();
                    }
#endif
                    String expr = extractJson(resp, "expression");
                    api.expression = expr;
                    updateEyes();
                }
            }
        }
    }
    http.end();
}

void handleSerialInput() {
    if (!Serial.available()) return;
    String input = Serial.readStringUntil('\n');
    input.trim();
    if (input.length() == 0) return;

    if (input == "/beep") { playNotify(); Serial.println("嘟！"); }
    else if (input == "/state") {
        api.refreshState();
        Serial.println("[好感:" + String(api.favorability) + " 阶段:" + api.favorStage + " 心情:" + String(api.mood) + "]");
    }
    else if (input == "/vol+") {
#if HAS_SPEAKER
        speaker.volume = min(4.0f, speaker.volume + 0.3f);
        Serial.println("音量: " + String(speaker.volume)); playNotify();
#endif
    }
    else if (input == "/vol-") {
#if HAS_SPEAKER
        speaker.volume = max(0.0f, speaker.volume - 0.3f);
        Serial.println("音量: " + String(speaker.volume)); playNotify();
#endif
    }
    else if (input == "/help") {
        Serial.println("直接输入 → 流式聊天（逐句播放）");
        Serial.println("/beep /state /vol+ /vol- /help");
    }
    else {
        streamChat(input);
    }
}

void setup() {
    Serial.begin(115200);
    delay(1000);
    Serial.println("\n=== Symbiosis AI 陪伴机器人 (ESP32-S3) ===");

#if HAS_SPEAKER
    speaker.begin();
    speaker.playTone(523, 150); delay(50);
    speaker.playTone(659, 150); delay(50);
    speaker.playTone(784, 300); speaker.stop();
#endif

#if HAS_EYES
    leftEye.setup(EYE_L_CS, EYE_L_RST); leftEye.init(); leftEye.setRotation(0); leftEye.setBrightness(200);
    rightEye.setup(EYE_R_CS, EYE_R_RST); rightEye.init(); rightEye.setRotation(0); rightEye.setBrightness(200);
    eyes.init(&leftEye, &rightEye); eyes.setExpression("expr_calm");
#endif

    connectWiFi();
    if (WiFi.status() == WL_CONNECTED) {
        api.initSSL();
        if (api.initUser()) {
            Serial.println("你好！我是" + api.characterName + "！");
            Serial.println("流式模式 | 逐句播放 | /help 帮助");
            api.refreshState();
            updateEyes();
            playNotify();
        }
    }
}

void loop() {
    if (WiFi.status() != WL_CONNECTED) { connectWiFi(); delay(5000); return; }

    handleSerialInput();
    pollForH5Reply();

#if HAS_EYES
    eyes.update();
#endif

    delay(10);
}

String extractJson(const String& json, const String& key) {
    String p = "\"" + key + "\":\"";
    int i = json.indexOf(p);
    if (i < 0) { p = "\"" + key + "\": \""; i = json.indexOf(p); }
    if (i < 0) return "";
    int s = i + p.length();
    int e = json.indexOf("\"", s);
    return e > s ? json.substring(s, e) : "";
}

#if HAS_SPEAKER
uint8_t* base64Decode(const String& input, size_t* outLen) {
    static const char b64[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    size_t len = input.length();
    *outLen = len * 3 / 4;
    uint8_t* out = (uint8_t*)ps_malloc(*outLen);
    if (!out) out = (uint8_t*)malloc(*outLen);
    if (!out) return NULL;
    size_t j = 0; uint32_t buf = 0; int bits = 0;
    for (size_t i = 0; i < len; i++) {
        char c = input[i]; if (c == '=') break;
        const char* p = strchr(b64, c); if (!p) continue;
        buf = (buf << 6) | (p - b64); bits += 6;
        if (bits >= 8) { bits -= 8; out[j++] = (buf >> bits) & 0xFF; }
    }
    *outLen = j; return out;
}
#endif
