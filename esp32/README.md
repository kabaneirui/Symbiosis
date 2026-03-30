# Symbiosis ESP32-S3 AI 陪伴机器人

## 硬件配置

| 组件 | 型号 |
|------|------|
| 主控 | ESP32-S3-WROOM-N8R8（8MB Flash + 8MB PSRAM） |
| 表情屏 | 1.28寸圆屏 GC9A01 240x240 — 显示 AI 表情 |
| 信息屏 | 1.28寸圆屏 GC9A01 240x240 — 显示好感度/心情等 |

## 接线参考

两块屏共用 SPI 总线（SCK + MOSI + DC），用不同 CS 引脚区分：

| 信号 | 表情屏引脚 | 信息屏引脚 | ESP32-S3 GPIO |
|------|-----------|-----------|---------------|
| SCK  | SCL/CLK   | SCL/CLK   | GPIO 12       |
| MOSI | SDA/DIN   | SDA/DIN   | GPIO 11       |
| DC   | DC        | DC        | GPIO 10       |
| CS   | CS        | -         | GPIO 9        |
| CS   | -         | CS        | GPIO 13       |
| RST  | RST       | -         | GPIO 14       |
| VCC  | 3.3V      | 3.3V      | 3V3           |
| GND  | GND       | GND       | GND           |

> 引脚可在 `src/config.h` 中修改

## 快速开始

1. 安装 [PlatformIO](https://platformio.org/)（VSCode 插件）
2. 修改 `src/config.h`：WiFi 密码 + 服务器 IP
3. 按上表接线
4. 连接 USB，PlatformIO → Upload
5. 打开串口监视器（115200），输入文字和 AI 聊天

## 串口命令

| 命令 | 说明 |
|------|------|
| 直接输入文字 | 和 AI 聊天（表情屏实时变化） |
| `/gift flower` | 送一束花 |
| `/gift star` | 送一颗星星 |
| `/state` | 查看当前状态 |
| `/help` | 帮助 |

## 屏幕功能

| 屏幕 | 显示内容 |
|------|---------|
| **表情屏** | 5 种表情自动切换：兴奋(金) / 开心(橙) / 平静(蓝灰) / 难过(深蓝) / 生气(红) |
| **信息屏** | 角色名 + 好感度 + 阶段 + 心情条 + AI 回复文本 |
