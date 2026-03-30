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

// 一体化语音聊天：发消息 → 后端LLM+TTS → 返回 MP3 直接播放
void speakChat(const String& message) {
    HTTPClient http;
    api.httpBegin(http, "/speak");
    http.addHeader("Content-Type", "application/json");
    http.setTimeout(30000);
    const char* headerKeys[] = {"X-Reply", "X-Favorability", "X-Expression"};
    http.collectHeaders(headerKeys, 3);

    String body = "{\"user_id\":" + String(api.userId) + ",\"message\":\"" + message + "\"}";
    int code = http.POST(body);

    if (code == 200) {
        // 从 header 读取文字回复
        String reply = http.header("X-Reply");
        String expr = http.header("X-Expression");
        Serial.println("小星: " + reply);
        api.expression = expr;
        updateEyes();

#if HAS_SPEAKER
        // body 就是 MP3 二进制流，直接下载播放
        int len = http.getSize();
        Serial.println("音频 " + String(len) + "B 下载中...");

        if (len > 100 && len < 500000) {
            uint8_t* buf = (uint8_t*)ps_malloc(len);
            if (!buf) buf = (uint8_t*)malloc(len);
            if (buf) {
                WiFiClient* stream = http.getStreamPtr();
                int downloaded = 0;
                unsigned long start = millis();
                while (downloaded < len && millis() - start < 20000) {
                    int avail = stream->available();
                    if (avail > 0) {
                        int toRead = min(avail, len - downloaded);
                        stream->readBytes(buf + downloaded, toRead);
                        downloaded += toRead;
                    }
                    delay(1);
                }
                Serial.println("下载完成 " + String(downloaded) + "B 播放...");
                speaker.playMp3(buf, downloaded);
                free(buf);
            } else {
                Serial.println("内存分配失败");
                playNotify();
            }
        } else {
            Serial.println("音频太小或太大: " + String(len));
            playNotify();
        }
#endif
    } else {
        Serial.println("语音聊天失败: " + String(code));
        // 回退普通文字聊天
        String reply = api.chat(message);
        Serial.println("小星: " + reply);
        playNotify();
    }
    http.end();
}

// 直接调火山引擎 TTS（H5 轮询时用）
void speakReply(const String& text) {
#if HAS_SPEAKER
    String shortText = text.substring(0, min((int)text.length(), 100));
    shortText.replace("\"", "");
    shortText.replace("\\", "");
    shortText.replace("\n", " ");

    WiFiClientSecure sslClient;
    sslClient.setInsecure();

    HTTPClient http;
    http.begin(sslClient, "https://openspeech.bytedance.com/api/v1/tts");
    http.addHeader("Content-Type", "application/json");
    http.addHeader("Authorization", "Bearer;" VOICE_TOKEN);
    http.setTimeout(15000);

    String body = "{\"app\":{\"appid\":\"" VOICE_APPID "\",\"token\":\"access_token\",\"cluster\":\"volcano_tts\"},"
        "\"user\":{\"uid\":\"esp32\"},"
        "\"audio\":{\"voice_type\":\"" VOICE_TYPE "\",\"encoding\":\"mp3\",\"speed_ratio\":1.0},"
        "\"request\":{\"reqid\":\"e" + String(millis()) + "\",\"text\":\"" + shortText + "\",\"text_type\":\"plain\",\"operation\":\"query\"}}";

    Serial.println("TTS 请求中...");
    int code = http.POST(body);

    if (code == 200) {
        String resp = http.getString();

        // 提取 base64 音频数据（在 "data" 字段）
        String audioB64 = extractJson(resp, "data");
        if (audioB64.length() > 100) {
            size_t audioLen = 0;
            uint8_t* audioData = base64Decode(audioB64, &audioLen);
            if (audioData && audioLen > 10) {
                Serial.println("播放 " + String(audioLen) + "B");
                speaker.playMp3(audioData, audioLen);
                free(audioData);
            }
        } else {
            Serial.println("TTS 无音频, resp: " + resp.substring(0, 100));
            playNotify();
        }
    } else {
        Serial.println("TTS 失败: " + String(code));
        playNotify();
    }
    http.end();
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
                    speakReply(reply);
#else
                    playNotify();
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
        Serial.println("你: " + input);
        speakChat(input);
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
