# Symbiosis AI 陪伴机器人 — 技术文档

> **版本**: v1.0  
> **日期**: 2026-03-28  
> **关联文档**: [GDD](./AI陪伴机器人养成游戏设计文档_GDD.md) · [开发工作计划](./AI陪伴机器人养成游戏_开发工作计划.md) · [任务清单](./开发任务清单_Checklist.md)

---

## 一、系统架构

```
┌─────────────────┐     ┌─────────────────┐
│  Unity App       │     │  ESP32-S3 机器人  │
│  (手机/PC)       │     │  (喇叭+麦+双眼)  │
└───────┬─────────┘     └───────┬─────────┘
        │ HTTPS                  │ HTTP (轮询)
        ▼                        ▼
┌──────────────────────────────────────────┐
│           Python 后端 (FastAPI)            │
│                                           │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ │
│  │ 对话服务   │ │ 养成系统  │ │ 语音服务  │ │
│  │ Prompt    │ │ 性格/好感 │ │ STT/TTS  │ │
│  └─────┬────┘ └──────────┘ └──────────┘ │
│        ▼                                  │
│  ┌──────────┐ ┌──────────┐               │
│  │ 豆包 LLM  │ │ SQLite   │               │
│  │ (火山方舟) │ │ (数据库)  │               │
│  └──────────┘ └──────────┘               │
└──────────────────────────────────────────┘
```

---

## 二、技术栈总览

| 层级 | 技术方案 | 说明 |
|------|---------|------|
| **Unity 客户端** | Unity 2019.4.40 + C# 7.3 | 聊天 UI / 送礼 / 状态展示 |
| **热更新** | HybridCLR v8.11.0 | C# 原生热更，UI 脚本可热更 |
| **后端框架** | Python + FastAPI + Uvicorn | 异步 HTTP 服务，12 个 API |
| **数据库** | SQLite + SQLAlchemy 2.x | 文件级数据库，可迁移 PostgreSQL |
| **大语言模型** | 豆包 doubao-1-5-pro-32k（火山方舟） | OpenAI 兼容接口格式 |
| **语音识别 STT** | faster-whisper (small, int8) | 本地运行，免费，支持中文 |
| **语音合成 TTS** | Edge TTS (zh-CN-XiaoyiNeural) | 微软免费中文女声 |
| **机器人主控** | ESP32-S3-WROOM-N8R8 (Waveshare Zero) | 8MB Flash + 8MB PSRAM |
| **喇叭** | MAX98357A I2S DAC + 3W 4Ω 喇叭 | GPIO 1/2/3 |
| **麦克风** | INMP441 I2S MEMS | GPIO 5/6/7 |
| **显示屏** | 2x GC9A01 1.28寸圆屏 240x240 | SPI，LovyanGFX 驱动（待接入） |
| **MP3 解码** | ESP8266Audio 库 | ESP32 端解码播放 TTS 音频 |

---

## 三、后端模块详细

### 3.1 项目结构

```
server/
├── main.py                 # FastAPI 入口，路由注册
├── config.py               # 环境配置（LLM/DB/语音）
├── database.py             # SQLAlchemy 连接 + 自动建表
├── .env                    # 密钥配置（不入版本控制）
├── requirements.txt        # Python 依赖
│
├── models/                 # 数据库 ORM 模型
│   ├── user.py             # 用户表
│   ├── character.py        # AI 角色（性格/情绪/好感/喜好）
│   ├── gift.py             # 送礼记录表
│   └── memory.py           # 记忆表 + 事件记录表
│
├── schemas/                # Pydantic 请求/响应模型
│   ├── chat.py             # 聊天
│   ├── gift.py             # 送礼
│   └── state.py            # 状态查询
│
├── services/               # 核心业务逻辑
│   ├── llm.py              # LLM API 调用（豆包/DeepSeek，OpenAI 兼容）
│   ├── prompt.py           # Prompt 模板 + 拼接引擎
│   ├── personality.py      # 性格变化 + 学习率衰减 + 每日上限
│   ├── mood.py             # 情绪自然衰减
│   ├── favorability.py     # 好感度衰减 + 阶段跃迁
│   ├── memory.py           # 短期记忆(20轮) + 长期记忆 + 召回
│   ├── events.py           # 7个里程碑事件 + 触发判定 + 选择分支
│   └── voice.py            # faster-whisper STT + Edge TTS
│
├── routers/                # API 路由
│   ├── user.py             # POST /user/init
│   ├── chat.py             # POST /chat
│   ├── gift.py             # POST /gift, GET /gifts
│   ├── state.py            # GET /state
│   ├── memory.py           # GET /memory
│   ├── events.py           # GET /events, POST /events/complete
│   ├── voice.py            # POST /voice/tts, POST /voice/chat
│   ├── robot.py            # GET /robot/poll（ESP32 轮询）
│   └── hotupdate.py        # GET /hotupdate/{filename}
│
├── data/
│   └── gift_config.json    # 15 种礼物配置
│
└── hotupdate/              # HybridCLR 热更 DLL 存放
```

### 3.2 API 接口清单

| 方法 | 路径 | 功能 | 调用方 |
|------|------|------|--------|
| `POST` | `/user/init` | 用户注册/登录（按昵称匹配） | Unity / ESP32 |
| `POST` | `/chat` | 文字聊天（含记忆召回 + Prompt + LLM） | Unity |
| `POST` | `/gift` | 送礼（好感计算 + 喜好匹配 + LLM 回复） | Unity |
| `GET` | `/gifts` | 获取礼物列表（15种） | Unity |
| `GET` | `/state` | 获取 AI 状态（好感/情绪/性格/表情） | Unity / ESP32 |
| `GET` | `/memory` | 获取记忆摘要 | Unity |
| `GET` | `/events` | 获取可触发事件列表 | Unity |
| `POST` | `/events/complete` | 完成事件 + 选择分支 | Unity |
| `POST` | `/voice/tts` | 文字转语音（返回 MP3 base64） | ESP32 |
| `POST` | `/voice/chat` | 语音聊天全链路（STT→AI→TTS） | ESP32 |
| `GET` | `/robot/poll` | 机器人轮询最新回复（含 TTS） | ESP32 |
| `GET` | `/hotupdate/{f}` | 热更 DLL 下载 | Unity |

### 3.3 数据库表结构

| 表名 | 核心字段 | 说明 |
|------|---------|------|
| **users** | id, nickname, created_at, last_active | 用户信息 |
| **ai_characters** | user_id, kindness/tsundere/humor/rational, mood, favorability, favor_stage, preferences(JSON) | AI 角色状态 |
| **gift_records** | user_id, character_id, gift_id, favor_gained, mood_change | 送礼历史 |
| **memories** | character_id, type(short/long), content, emotional_weight | 记忆存储 |
| **event_records** | character_id, event_id, completed, choice | 事件完成记录 |

### 3.4 Prompt 系统

```
┌─────────────────────────────────────┐
│           Prompt 构建                │
│                                     │
│  ① 系统设定（角色身份）               │
│  ② 性格描述（自然语言，非数值）        │
│  ③ 情绪指令（根据 mood 值生成）       │
│  ④ 关系阶段指令（陌生→熟悉→依赖→亲密） │
│  ⑤ 长期记忆（按权重排序，前8条）       │
│  ⑥ 近期对话（最近10轮）               │
│  ⑦ 核心规则（不做助手、有个性...）     │
│                                     │
│  Token 预算：记忆部分 ≤ 800 字符       │
└─────────────────────────────────────┘
```

### 3.5 语音服务

| 模块 | 方案 | 说明 |
|------|------|------|
| **STT** | faster-whisper (small, int8, CPU) | 本地运行，`initial_prompt="以下是普通话的句子。"` 提升中文准确度，`vad_filter=True` 过滤静音 |
| **TTS** | edge-tts (`zh-CN-XiaoyiNeural`) | 微软免费中文女声，异步流式生成 MP3 |

---

## 四、Unity 客户端模块

### 4.1 项目结构

```
Assets/
├── Scripts/                        # Framework 程序集（不热更）
│   ├── Framework.asmdef
│   ├── Loader.cs                   # 热更加载器（下载 DLL → Assembly.Load）
│   ├── Models/ApiModels.cs         # 请求/响应数据结构
│   ├── Network/ApiClient.cs        # HTTP 封装（UnityWebRequest + TaskCompletionSource）
│   └── Services/
│       ├── GameManager.cs          # 全局单例（ApiClient + 状态缓存）
│       └── UIManager.cs            # UI 管理器（Resources 加载 + EventSystem 保障）
│
├── HotUpdate/                      # HotUpdate 程序集（可热更）
│   ├── HotUpdate.asmdef            # 引用 Framework
│   ├── LoginUI.cs                  # 登录界面（热更入口）
│   ├── ChatUI.cs                   # 聊天界面（气泡列表 + 事件检测）
│   ├── GiftPanelUI.cs              # 礼物面板（网格选礼）
│   ├── StatusBarUI.cs              # 状态栏（好感/阶段/心情）
│   ├── ExpressionUI.cs             # 表情切换
│   └── EventPanelUI.cs             # 事件弹窗 + 选择分支
│
├── Editor/
│   ├── UIPrefabGenerator.cs        # 一键生成 7 个 UI Prefab
│   └── ExpressionGenerator.cs      # 一键生成 5 组表情图片
│
├── Resources/UI/                   # UI Prefab（代码生成）
│   ├── LoginPanel / ChatPanel / StatusBar
│   ├── GiftPanel / GiftItem
│   └── UserBubble / AIBubble
│
└── Packages/
    └── com.code-philosophy.hybridclr/  # HybridCLR v8.11.0
```

### 4.2 热更新方案

| 项目 | 方案 |
|------|------|
| 框架 | HybridCLR v8.11.0（IL2CPP 解释器扩展） |
| 不热更 | Loader / GameManager / ApiClient / ApiModels |
| 可热更 | 所有 UI 脚本（LoginUI / ChatUI / GiftPanelUI ...） |
| 加载方式 | 真机：启动时从服务器下载 HotUpdate.dll → Assembly.Load |
| 编辑器 | Unity 直接编译，无需手动加载 |
| 资源 | 当前用 Resources（不可热更），后续可迁移 AssetBundle |

### 4.3 UI 加载方式

所有 UI 界面通过 `UIManager.Open("PrefabName")` 动态加载，不在场景中预先摆放。各脚本通过 `Init()` 方法自动查找子节点 UI 组件（递归搜索），同时兼容 Inspector 拖拽模式。

---

## 五、ESP32 机器人模块

### 5.1 项目结构

```
esp32/
├── platformio.ini          # PlatformIO 工程配置
└── src/
    ├── config.h            # WiFi / 引脚 / 功能开关 / VAD 参数
    ├── main.cpp            # 主循环：WiFi → 轮询 → 播放
    ├── api_client.h        # HTTP 封装（聊天/送礼/状态）
    ├── audio.h             # I2S 喇叭 + MP3 解码 + 麦克风录音
    └── display.h           # 双圆屏驱动 + 5种表情 + 眨眼 + 视线
```

### 5.2 硬件配置

| 组件 | 型号 | 引脚 |
|------|------|------|
| 主控 | ESP32-S3-WROOM-N8R8 (Waveshare Zero) | USB-C |
| 喇叭 | MAX98357A + 3W/4Ω | BCLK=GPIO1, LRC=GPIO2, DIN=GPIO3 |
| 麦克风 | INMP441 | SCK=GPIO5, WS=GPIO6, SD=GPIO7 |
| 左眼屏 | GC9A01 1.28" 240x240 | SCK=12, MOSI=11, DC=10, CS=9, RST=14 |
| 右眼屏 | GC9A01 1.28" 240x240 | CS=13（共用 SPI 总线） |

### 5.3 工作模式

```
启动 → WiFi 连接 → 用户初始化 → 启动音
                                    │
               ┌────────────────────┼────────────────────┐
               ▼                    ▼                    ▼
         串口命令处理           轮询 /robot/poll        眼睛动画
         (聊天/送礼/音量)       (每2秒一次)            (眨眼/视线)
                                    │
                              有新回复？
                              是 → 播放 TTS MP3
                              否 → 跳过
```

### 5.4 I2S 资源管理

喇叭和麦克风共用 `I2S_NUM_0`（ESP32-S3-Zero 的 I2S_NUM_1 不可用），通过交替安装/卸载 I2S 驱动实现切换：

```
播放模式：I2S_NUM_0 配置为 TX → MAX98357A
录音模式：卸载 TX → I2S_NUM_0 配置为 RX → INMP441 → 录完卸载 → 恢复 TX
```

### 5.5 依赖库

| 库 | 版本 | 用途 |
|----|------|------|
| ArduinoJson | ^7 | JSON 解析 |
| LovyanGFX | ^1 | 圆屏驱动（GC9A01） |
| ESP8266Audio | ^1 | MP3 解码 + I2S 播放 |

---

## 六、核心数据流

### 6.1 文字聊天流程

```
Unity 输入消息
    ↓ POST /chat
后端：加载用户数据 → 情绪衰减 → 召回记忆 → 拼接 Prompt
    ↓ 调用豆包 API
AI 生成回复
    ↓
后端：存记忆 + 更新好感/情绪 + push_reply()
    ↓                              ↓
Unity 显示文字              ESP32 轮询 /robot/poll
                                    ↓
                            后端生成 TTS (Edge TTS)
                                    ↓
                            ESP32 解码 MP3 → 喇叭播放
```

### 6.2 送礼流程

```
Unity 选择礼物
    ↓ POST /gift
后端：查礼物配置 → 匹配喜好标签 → 计算好感增益
    ↓
    首次赠送 +20% / 重复>5次 衰减10%/次
    ↓
    更新好感度 + 情绪 + 写记忆
    ↓ 调用豆包 API（送礼场景 Prompt）
AI 生成差异化回复
    ↓
返回：回复文本 + 好感变化 + 情绪变化 + 表情
```

### 6.3 好感公式

```
likeScore = max(preference[tag] for tag in gift.tags)
finalFavor = baseFavor × (1.0 + likeScore)

if 首次赠送: finalFavor × 1.2
if 重复 > 5次: finalFavor × max(0.5, 1.0 - (count-5) × 0.1)
```

---

## 七、配置说明

### 7.1 后端 `.env`

```env
LLM_BASE_URL=https://ark.cn-beijing.volces.com/api/v3
LLM_API_KEY=<你的豆包API Key>
LLM_MODEL=doubao-1-5-pro-32k-250115
DATABASE_URL=sqlite:///./symbiosis.db
```

### 7.2 ESP32 `config.h`

```c
#define WIFI_SSID     "你的WiFi"
#define WIFI_PASSWORD "WiFi密码"
#define SERVER_URL    "http://电脑局域网IP:8000"
```

### 7.3 启动命令

```bash
# 后端
cd ~/Symbiosis/server && source venv/bin/activate
export PATH="/opt/homebrew/bin:$PATH"
python -m uvicorn main:app --host 0.0.0.0 --port 8000

# ESP32 烧录
cd ~/Symbiosis/esp32
pio run --target upload --upload-port /dev/cu.usbmodem2101
```

---

## 八、待完成项

| 模块 | 内容 | 状态 |
|------|------|------|
| 圆屏双眼 | 接入 GC9A01 + 5种表情 + 眨眼动画 | 代码就绪，待硬件 |
| 语音打断 | 播放时实时检测麦克风打断 | 需圆屏后 I2S 分离 |
| 语音输入 | 麦克风录音 → STT 唤醒 | 需改善硬件连接 |
| AssetBundle | UI Prefab 资源热更 | MVP 后迁移 |
| 部署 | 后端部署到云服务器 + PostgreSQL | 上线前 |
