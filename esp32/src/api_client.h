#ifndef API_CLIENT_H
#define API_CLIENT_H

#include <Arduino.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include "config.h"

class ApiClient {
public:
    int userId = -1;
    String characterName;
    int favorability = 0;
    String favorStage;
    float mood = 0;
    String expression;
    WiFiClientSecure _sslClient;

    void initSSL() {
        _sslClient.setInsecure();
    }

    void httpBegin(HTTPClient& http, const String& path) {
        String url = String(SERVER_URL) + path;
        if (url.startsWith("https")) {
            http.begin(_sslClient, url);
        } else {
            http.begin(url);
        }
    }

    bool initUser() {
        HTTPClient http;
        httpBegin(http, "/user/init");
        http.addHeader("Content-Type", "application/json");
        http.setTimeout(15000);

        String body = "{\"nickname\":\"" + String(USER_NICKNAME) + "\"}";
        Serial.println("初始化请求: " + String(SERVER_URL) + "/user/init");
        int code = http.POST(body);
        Serial.println("HTTP 响应码: " + String(code));

        if (code == 200) {
            JsonDocument doc;
            deserializeJson(doc, http.getString());
            userId = doc["user_id"].as<int>();
            characterName = doc["character_name"].as<String>();
            Serial.println("用户初始化: ID=" + String(userId) + " 角色=" + characterName);
            http.end();
            return true;
        }

        Serial.println("用户初始化失败: " + String(code));
        http.end();
        return false;
    }

    String chat(const String& message) {
        if (userId < 0) return "未登录";

        HTTPClient http;
        httpBegin(http, "/chat");
        http.addHeader("Content-Type", "application/json");
        http.setTimeout(30000);

        String body = "{\"user_id\":" + String(userId) + ",\"message\":\"" + escapeJson(message) + "\"}";
        int code = http.POST(body);

        if (code == 200) {
            JsonDocument doc;
            deserializeJson(doc, http.getString());
            String reply = doc["reply"].as<String>();
            favorability = doc["favorability"].as<int>();
            favorStage = doc["favor_stage"].as<String>();
            mood = doc["mood"].as<float>();
            expression = doc["expression"].as<String>();
            http.end();
            return reply;
        }

        http.end();
        return "连接失败";
    }

    String sendGift(const String& giftId) {
        if (userId < 0) return "未登录";

        HTTPClient http;
        httpBegin(http, "/gift");
        http.addHeader("Content-Type", "application/json");
        http.setTimeout(30000);

        String body = "{\"user_id\":" + String(userId) + ",\"gift_id\":\"" + giftId + "\"}";
        int code = http.POST(body);

        if (code == 200) {
            JsonDocument doc;
            deserializeJson(doc, http.getString());
            String reply = doc["reply"].as<String>();
            favorability = doc["favorability"].as<int>();
            mood = doc["mood"].as<float>();
            expression = doc["expression"].as<String>();
            http.end();
            return reply;
        }

        http.end();
        return "送礼失败";
    }

    bool refreshState() {
        if (userId < 0) return false;

        HTTPClient http;
        httpBegin(http, "/state?user_id=" + String(userId));
        int code = http.GET();

        if (code == 200) {
            JsonDocument doc;
            deserializeJson(doc, http.getString());
            favorability = doc["favorability"].as<int>();
            favorStage = doc["favor_stage"].as<String>();
            mood = doc["mood"].as<float>();
            expression = doc["expression"].as<String>();
            http.end();
            return true;
        }

        http.end();
        return false;
    }

private:
    String escapeJson(const String& s) {
        String result = s;
        result.replace("\\", "\\\\");
        result.replace("\"", "\\\"");
        result.replace("\n", "\\n");
        return result;
    }
};

#endif
