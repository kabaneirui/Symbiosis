#include <Arduino.h>
#include <WiFi.h>
#include <driver/i2s.h>
#include <HTTPClient.h>
#include <WebSocketsClient.h>
#include "config.h"
#include "api_client.h"

#if HAS_EYES
#include "display.h"
EyeDisplay leftEye;
EyeDisplay rightEye;
EyeRenderer eyes;
unsigned long lastLookChange = 0;
#endif

#if HAS_SPEAKER
#include "audio.h"
AudioPlayer speaker;
#endif

ApiClient api;
WebSocketsClient webSocket;
bool wsConnected = false;

#if HAS_SPEAKER
uint8_t* base64Decode(const String& input, size_t* outLen);
#endif
String extractJson(const String& json, const String& key);

void connectWiFi() {
    Serial.print("连接 WiFi: " + String(WIFI_SSID));
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    int attempts = 0;
    while (WiFi.status() != WL_CONNECTED && attempts < 30) {
        delay(500);
        Serial.print(".");
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

// WebSocket 事件处理
void webSocketEvent(WStype_t type, uint8_t* payload, size_t length) {
    switch (type) {
        case WStype_CONNECTED:
            wsConnected = true;
            Serial.println("WebSocket 已连接");
            break;

        case WStype_DISCONNECTED:
            wsConnected = false;
            Serial.println("WebSocket 断开，自动重连...");
            break;

        case WStype_TEXT: {
            String msg = String((char*)payload);

            // 判断消息类型
            if (msg.indexOf("\"type\":\"reply\"") >= 0 || msg.indexOf("\"type\": \"reply\"") >= 0) {
                // HTTP 模式的完整回复推送
                String reply = extractJson(msg, "reply");
                String expression = extractJson(msg, "expression");
                Serial.println("小星: " + reply);
                api.expression = expression;
                updateEyes();

#if HAS_SPEAKER
                String audioB64 = extractJson(msg, "audio_base64");
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
            else if (msg.indexOf("\"type\":\"reply_chunk\"") >= 0 || msg.indexOf("\"type\": \"reply_chunk\"") >= 0) {
                // 流式文本片段
                String text = extractJson(msg, "text");
                Serial.print(text);
            }
            else if (msg.indexOf("\"type\":\"audio_chunk\"") >= 0 || msg.indexOf("\"type\": \"audio_chunk\"") >= 0) {
                // 流式音频片段 — 收到就播放
#if HAS_SPEAKER
                String audioB64 = extractJson(msg, "audio_base64");
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
            else if (msg.indexOf("\"type\":\"reply_done\"") >= 0 || msg.indexOf("\"type\": \"reply_done\"") >= 0) {
                // 回复完成
                Serial.println();
                String expression = extractJson(msg, "expression");
                api.expression = expression;
                updateEyes();
                Serial.println("[回复完毕]");
            }
            else if (msg.indexOf("\"type\":\"pong\"") >= 0) {
                // 心跳响应
            }
            break;
        }

        default:
            break;
    }
}

void connectWebSocket() {
    String url = String(SERVER_URL);
    bool useSSL = url.startsWith("https");
    url.replace("https://", "");
    url.replace("http://", "");

    // 去掉尾部斜杠
    if (url.endsWith("/")) url = url.substring(0, url.length() - 1);

    int colonIdx = url.indexOf(":");
    String host;
    int port;

    if (colonIdx > 0) {
        host = url.substring(0, colonIdx);
        port = url.substring(colonIdx + 1).toInt();
    } else {
        host = url;
        port = useSSL ? 443 : 80;
    }

    Serial.println("连接 WebSocket: " + host + ":" + String(port) + (useSSL ? " (SSL)" : ""));

    if (useSSL) {
        webSocket.beginSSL(host.c_str(), port, "/ws/robot");
    } else {
        webSocket.begin(host.c_str(), port, "/ws/robot");
    }
    webSocket.onEvent(webSocketEvent);
    webSocket.setReconnectInterval(3000);
}

void handleSerialInput() {
    if (!Serial.available()) return;
    String input = Serial.readStringUntil('\n');
    input.trim();
    if (input.length() == 0) return;

    if (input == "/beep") {
        playNotify();
        Serial.println("嘟！");
    }
    else if (input == "/state") {
        api.refreshState();
        Serial.println("[好感:" + String(api.favorability) + " 阶段:" + api.favorStage +
                       " 心情:" + String(api.mood) + " 表情:" + api.expression + "]");
    }
    else if (input == "/vol+") {
#if HAS_SPEAKER
        speaker.volume = min(4.0f, speaker.volume + 0.3f);
        Serial.println("音量: " + String(speaker.volume));
        playNotify();
#endif
    }
    else if (input == "/vol-") {
#if HAS_SPEAKER
        speaker.volume = max(0.0f, speaker.volume - 0.3f);
        Serial.println("音量: " + String(speaker.volume));
        playNotify();
#endif
    }
    else if (input.startsWith("/vol ")) {
#if HAS_SPEAKER
        float v = input.substring(5).toFloat();
        if (v >= 0 && v <= 4.0) {
            speaker.volume = v;
            Serial.println("音量: " + String(v));
            playNotify();
        }
#endif
    }
    else if (input == "/ws") {
        Serial.println("WebSocket: " + String(wsConnected ? "已连接" : "未连接"));
    }
    else if (input == "/help") {
        Serial.println("命令:");
        Serial.println("  直接输入      → 通过 WebSocket 聊天");
        Serial.println("  /beep        → 测试喇叭");
        Serial.println("  /state       → 查看状态");
        Serial.println("  /vol+/-      → 调节音量");
        Serial.println("  /ws          → 查看 WebSocket 状态");
        Serial.println("  /help        → 帮助");
        Serial.println("");
        Serial.println("在 Unity App 中聊天，机器人实时说出回复");
    }
    else {
        // 通过 WebSocket 发送聊天
        if (wsConnected) {
            Serial.println("你: " + input);
            String chatMsg = "{\"type\":\"chat\",\"user_id\":" + String(api.userId) +
                             ",\"message\":\"" + input + "\"}";
            webSocket.sendTXT(chatMsg);
        } else {
            // WebSocket 没连上，走 HTTP
            Serial.println("你: " + input);
            String reply = api.chat(input);
            Serial.println("小星: " + reply);
            playNotify();
        }
    }
}

void setup() {
    Serial.begin(115200);
    delay(1000);
    Serial.println("\n=== Symbiosis AI 陪伴机器人 (ESP32-S3) ===");

#if HAS_SPEAKER
    speaker.begin();
    speaker.playTone(523, 150);
    delay(50);
    speaker.playTone(659, 150);
    delay(50);
    speaker.playTone(784, 300);
    speaker.stop();
#endif

#if HAS_EYES
    leftEye.setup(EYE_L_CS, EYE_L_RST);
    leftEye.init();
    leftEye.setRotation(0);
    leftEye.setBrightness(200);
    rightEye.setup(EYE_R_CS, EYE_R_RST);
    rightEye.init();
    rightEye.setRotation(0);
    rightEye.setBrightness(200);
    eyes.init(&leftEye, &rightEye);
    eyes.setExpression("expr_calm");
#endif

    connectWiFi();

    if (WiFi.status() == WL_CONNECTED) {
        api.initSSL();
        if (api.initUser()) {
            Serial.println("你好！我是" + api.characterName + "！");
            api.refreshState();
            updateEyes();
            playNotify();
        }
        // WebSocket 在 Railway 免费版不稳定，改用 HTTP 轮询
        // connectWebSocket();
        Serial.println("HTTP 轮询模式 | 手机 H5 聊天实时播报 | /help 帮助");
    }
}

void loop() {
    if (WiFi.status() != WL_CONNECTED) {
        connectWiFi();
        delay(5000);
        return;
    }

    handleSerialInput();

    // HTTP 轮询
    static unsigned long lastPoll = 0;
    static int lastReplyId = 0;
    if (millis() - lastPoll > 5000) {
        lastPoll = millis();
        HTTPClient http;
        api.httpBegin(http, "/robot/poll?since_id=" + String(lastReplyId));
        http.setTimeout(10000);
        int code = http.GET();
        Serial.println("[轮询] code=" + String(code));
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
                    String expr = extractJson(resp, "expression");
                    if (reply.length() > 0) {
                        Serial.println("小星: " + reply);
                        api.expression = expr;
                        updateEyes();
#if HAS_SPEAKER
                        String audioB64 = extractJson(resp, "audio_base64");
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
                }
            }
        }
        http.end();
    }

#if HAS_EYES
    eyes.update();
#endif

    delay(5);
}

String extractJson(const String& json, const String& key) {
    String pattern = "\"" + key + "\":\"";
    int idx = json.indexOf(pattern);
    if (idx < 0) {
        pattern = "\"" + key + "\": \"";
        idx = json.indexOf(pattern);
    }
    if (idx < 0) return "";
    int start = idx + pattern.length();
    int end = json.indexOf("\"", start);
    if (end < 0) return "";
    return json.substring(start, end);
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
