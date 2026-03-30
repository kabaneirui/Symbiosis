#ifndef DISPLAY_H
#define DISPLAY_H

#include <LovyanGFX.hpp>
#include "config.h"

// ========== 屏幕驱动 ==========

class EyeDisplay : public lgfx::LGFX_Device {
    lgfx::Panel_GC9A01 _panel;
    lgfx::Bus_SPI _bus;
public:
    void setup(int cs, int rst) {
        auto cfg = _bus.config();
        cfg.spi_host = SPI2_HOST;
        cfg.freq_write = 40000000;
        cfg.pin_sclk = EYE_L_SCK;
        cfg.pin_mosi = EYE_L_MOSI;
        cfg.pin_dc   = EYE_L_DC;
        _bus.config(cfg);
        _panel.setBus(&_bus);

        auto pcfg = _panel.config();
        pcfg.pin_cs  = cs;
        pcfg.pin_rst = rst;
        pcfg.memory_width  = SCREEN_W;
        pcfg.memory_height = SCREEN_H;
        pcfg.panel_width   = SCREEN_W;
        pcfg.panel_height  = SCREEN_H;
        _panel.config(pcfg);
        setPanel(&_panel);
    }
};

// ========== 眼睛表情参数 ==========

struct EyeStyle {
    uint16_t bgColor;       // 眼白/背景色
    uint16_t irisColor;     // 虹膜颜色
    uint16_t pupilColor;    // 瞳孔颜色
    int irisRadius;         // 虹膜半径
    int pupilRadius;        // 瞳孔半径
    float eyeOpenRatio;     // 眼睛睁开比例 0~1 (1=全开, 0=闭合)
    int eyebrowAngle;       // 眉毛角度 (-30=怒, 0=正常, 15=忧)
    bool hasHighlight;      // 是否有高光点
    bool hasTears;          // 是否有泪滴
};

// ========== 双眼渲染器 ==========

class EyeRenderer {
public:
    EyeDisplay* leftLcd;
    EyeDisplay* rightLcd;

    int lookX = 0;        // 视线偏移 -30 ~ 30
    int lookY = 0;
    bool isBlinking = false;
    String currentExpr = "";

    unsigned long nextBlinkTime = 0;
    unsigned long blinkEndTime = 0;

    void init(EyeDisplay* left, EyeDisplay* right) {
        leftLcd = left;
        rightLcd = right;
        nextBlinkTime = millis() + random(BLINK_INTERVAL_MIN, BLINK_INTERVAL_MAX);
    }

    void setExpression(const String& expr) {
        if (expr == currentExpr) return;
        currentExpr = expr;
        drawBothEyes();
    }

    void update() {
        unsigned long now = millis();

        // 自动眨眼
        if (!isBlinking && now >= nextBlinkTime) {
            isBlinking = true;
            blinkEndTime = now + BLINK_DURATION;
            drawBothEyes();
        }
        if (isBlinking && now >= blinkEndTime) {
            isBlinking = false;
            nextBlinkTime = now + random(BLINK_INTERVAL_MIN, BLINK_INTERVAL_MAX);
            drawBothEyes();
        }
    }

    void lookAt(int x, int y) {
        x = constrain(x, -30, 30);
        y = constrain(y, -30, 30);
        if (x != lookX || y != lookY) {
            lookX = x;
            lookY = y;
            drawBothEyes();
        }
    }

    void lookRandom() {
        lookAt(random(-20, 20), random(-15, 15));
    }

private:
    EyeStyle getStyle() {
        EyeStyle s;
        s.hasHighlight = true;
        s.hasTears = false;
        s.eyebrowAngle = 0;

        if (currentExpr == "expr_excited") {
            s.bgColor = leftLcd->color565(255, 255, 255);
            s.irisColor = leftLcd->color565(255, 200, 50);
            s.pupilColor = leftLcd->color565(40, 30, 10);
            s.irisRadius = 55;
            s.pupilRadius = 22;
            s.eyeOpenRatio = 1.0;
            s.eyebrowAngle = -5;
        }
        else if (currentExpr == "expr_happy") {
            s.bgColor = leftLcd->color565(255, 255, 255);
            s.irisColor = leftLcd->color565(100, 180, 255);
            s.pupilColor = leftLcd->color565(20, 40, 80);
            s.irisRadius = 50;
            s.pupilRadius = 20;
            s.eyeOpenRatio = 0.7;  // 半眯 = 开心
            s.eyebrowAngle = -5;
        }
        else if (currentExpr == "expr_sad") {
            s.bgColor = leftLcd->color565(230, 235, 245);
            s.irisColor = leftLcd->color565(80, 100, 160);
            s.pupilColor = leftLcd->color565(30, 35, 60);
            s.irisRadius = 45;
            s.pupilRadius = 22;
            s.eyeOpenRatio = 0.85;
            s.eyebrowAngle = 15;  // 眉毛耷拉
            s.hasTears = true;
        }
        else if (currentExpr == "expr_angry") {
            s.bgColor = leftLcd->color565(255, 240, 240);
            s.irisColor = leftLcd->color565(200, 60, 50);
            s.pupilColor = leftLcd->color565(60, 10, 10);
            s.irisRadius = 48;
            s.pupilRadius = 18;
            s.eyeOpenRatio = 0.75;
            s.eyebrowAngle = -25; // 怒眉
        }
        else {
            // calm
            s.bgColor = leftLcd->color565(255, 255, 255);
            s.irisColor = leftLcd->color565(90, 140, 200);
            s.pupilColor = leftLcd->color565(25, 35, 55);
            s.irisRadius = 48;
            s.pupilRadius = 20;
            s.eyeOpenRatio = 0.9;
            s.eyebrowAngle = 0;
        }

        if (isBlinking) {
            s.eyeOpenRatio = 0.05;
        }

        return s;
    }

    void drawBothEyes() {
        EyeStyle style = getStyle();
        drawEye(leftLcd, style, false);
        drawEye(rightLcd, style, true);
    }

    void drawEye(EyeDisplay* lcd, EyeStyle& s, bool isRight) {
        int cx = SCREEN_W / 2;
        int cy = SCREEN_H / 2;
        int eyeW = SCREEN_W / 2 - 10;

        // 黑色背景
        lcd->fillScreen(TFT_BLACK);

        // 眼白（椭圆，高度受 eyeOpenRatio 控制）
        int eyeH = (int)(eyeW * s.eyeOpenRatio);
        if (eyeH < 5) {
            // 闭眼 — 画一条线
            lcd->drawLine(cx - eyeW, cy, cx + eyeW, cy, s.bgColor);
            lcd->drawLine(cx - eyeW, cy + 1, cx + eyeW, cy + 1, s.bgColor);
            return;
        }
        lcd->fillEllipse(cx, cy, eyeW, eyeH, s.bgColor);

        // 虹膜（随视线偏移）
        int irisX = cx + lookX;
        int irisY = cy + lookY;
        // 限制虹膜不超出眼白范围
        int maxOffX = eyeW - s.irisRadius - 5;
        int maxOffY = eyeH - s.irisRadius - 5;
        irisX = constrain(irisX, cx - maxOffX, cx + maxOffX);
        irisY = constrain(irisY, cy - maxOffY, cy + maxOffY);

        lcd->fillCircle(irisX, irisY, s.irisRadius, s.irisColor);

        // 瞳孔
        lcd->fillCircle(irisX, irisY, s.pupilRadius, s.pupilColor);

        // 高光
        if (s.hasHighlight) {
            lcd->fillCircle(irisX - 12, irisY - 12, 6, TFT_WHITE);
            lcd->fillCircle(irisX + 6, irisY + 6, 3, TFT_WHITE);
        }

        // 眉毛
        int browY = cy - eyeH - 10;
        int browLen = 50;
        int mirrorSign = isRight ? -1 : 1;
        int browEndY = browY + s.eyebrowAngle * mirrorSign / 3;
        lcd->drawLine(cx - browLen / 2, browY, cx + browLen / 2, browEndY, TFT_WHITE);
        lcd->drawLine(cx - browLen / 2, browY + 1, cx + browLen / 2, browEndY + 1, TFT_WHITE);
        lcd->drawLine(cx - browLen / 2, browY + 2, cx + browLen / 2, browEndY + 2, TFT_WHITE);

        // 泪滴
        if (s.hasTears && !isRight) {
            int tearX = cx + eyeW / 2;
            int tearY = cy + eyeH;
            lcd->fillCircle(tearX, tearY + 8, 4, lcd->color565(100, 160, 240));
            lcd->fillCircle(tearX, tearY + 18, 3, lcd->color565(100, 160, 240));
        }

        // 眼底弧线（增加立体感）
        if (s.eyeOpenRatio > 0.3) {
            lcd->drawEllipse(cx, cy, eyeW, eyeH, lcd->color565(180, 180, 190));
        }
    }
};

#endif
