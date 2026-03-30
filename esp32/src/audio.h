#ifndef AUDIO_H
#define AUDIO_H

#include <Arduino.h>
#include <driver/i2s.h>
#include "config.h"

#include <AudioFileSourcePROGMEM.h>
#include <AudioFileSourceBuffer.h>
#include <AudioGeneratorMP3.h>
#include <AudioOutputI2S.h>

// 共用 I2S_NUM_0，交替切换（先关一个再开另一个）
#define I2S_PORT_SPEAKER I2S_NUM_0
#define I2S_PORT_MIC     I2S_NUM_0

class AudioPlayer {
public:
    bool _running = false;
    float volume = 1.5;  // 音量 0.0~4.0

    bool begin() {
        if (_running) return true;
        i2s_config_t cfg = {};
        cfg.mode = (i2s_mode_t)(I2S_MODE_MASTER | I2S_MODE_TX);
        cfg.sample_rate = 44100;
        cfg.bits_per_sample = I2S_BITS_PER_SAMPLE_16BIT;
        cfg.channel_format = I2S_CHANNEL_FMT_ONLY_LEFT;
        cfg.communication_format = I2S_COMM_FORMAT_STAND_I2S;
        cfg.intr_alloc_flags = ESP_INTR_FLAG_LEVEL1;
        cfg.dma_buf_count = 8;
        cfg.dma_buf_len = 64;
        cfg.use_apll = false;

        i2s_pin_config_t pins = {};
        pins.bck_io_num = SPK_BCLK;
        pins.ws_io_num = SPK_LRC;
        pins.data_out_num = SPK_DIN;
        pins.data_in_num = I2S_PIN_NO_CHANGE;

        esp_err_t err = i2s_driver_install(I2S_PORT_SPEAKER, &cfg, 0, NULL);
        if (err != ESP_OK) {
            Serial.println("喇叭 I2S 初始化失败: " + String(err));
            return false;
        }
        i2s_set_pin(I2S_PORT_SPEAKER, &pins);
        i2s_zero_dma_buffer(I2S_PORT_SPEAKER);
        _running = true;
        Serial.println("喇叭初始化完成");
        return true;
    }

    void playRaw(const uint8_t* data, size_t len) {
        size_t written = 0;
        i2s_write(I2S_PORT_SPEAKER, data, len, &written, portMAX_DELAY);
    }

    void playWav(const uint8_t* wavData, size_t wavLen) {
        if (wavLen < 44) return;
        // 跳过 44 字节 WAV 头
        playRaw(wavData + 44, wavLen - 44);
    }

    void playTone(int freq, int durationMs) {
        const int sr = 44100;
        int samples = sr * durationMs / 1000;
        int16_t* buf = (int16_t*)malloc(samples * sizeof(int16_t));
        if (!buf) return;

        for (int i = 0; i < samples; i++) {
            buf[i] = (int16_t)(sin(2.0 * PI * freq * i / sr) * 8000 * volume / 1.5);
        }

        size_t written;
        i2s_write(I2S_PORT_SPEAKER, buf, samples * sizeof(int16_t), &written, portMAX_DELAY);
        free(buf);
    }

    void stop() {
        i2s_zero_dma_buffer(I2S_PORT_SPEAKER);
    }

    void pause() {
        if (!_running) return;
        i2s_driver_uninstall(I2S_PORT_SPEAKER);
        _running = false;
    }

    void resume() {
        begin();
    }

    // 播放 mp3 数据（使用 ESP8266Audio 库）
    void playMp3(const uint8_t* data, size_t len) {
        if (!data || len < 10) return;

        // 先确保 I2S 没被占用
        if (_running) {
            i2s_driver_uninstall(I2S_PORT_SPEAKER);
            _running = false;
        }

        // 使用 ESP8266Audio 播放
        AudioFileSourceBuffer* bufSrc = NULL;
        AudioFileSourcePROGMEM* src = new AudioFileSourcePROGMEM(data, len);
        bufSrc = new AudioFileSourceBuffer(src, 2048);
        AudioOutputI2S* out = new AudioOutputI2S();
        out->SetPinout(SPK_BCLK, SPK_LRC, SPK_DIN);
        out->SetGain(volume);
        AudioGeneratorMP3* mp3 = new AudioGeneratorMP3();

        Serial.println("播放语音回复...（按回车打断）");
        mp3->begin(bufSrc, out);
        while (mp3->isRunning()) {
            if (!mp3->loop()) mp3->stop();
            // 检查串口是否有输入（打断播放）
            if (Serial.available()) {
                Serial.readStringUntil('\n');
                Serial.println("[已打断]");
                mp3->stop();
                break;
            }
        }
        Serial.println("语音播放完毕");

        delete mp3;
        delete out;
        delete bufSrc;
        delete src;

        // 播完后恢复简单 I2S（用于提示音）
        delay(100);
        begin();
    }
};

class AudioRecorder {
public:
    uint8_t* buffer = NULL;
    size_t bufferSize = 0;
    size_t recordedSize = 0;
    bool _installed = false;

    bool begin() {
        bufferSize = SAMPLE_RATE * 2 * RECORD_SECONDS;
        buffer = (uint8_t*)ps_malloc(bufferSize);
        if (!buffer) buffer = (uint8_t*)malloc(bufferSize);
        if (!buffer) {
            Serial.println("录音缓冲区分配失败");
            return false;
        }
        Serial.println("麦克风准备就绪 (缓冲区: " + String(bufferSize / 1024) + "KB)");
        return true;
    }

    void startRecording() {
        // 录音时才初始化 I2S，避免和喇叭冲突
        if (!_installed) {
            i2s_config_t cfg = {};
            cfg.mode = (i2s_mode_t)(I2S_MODE_MASTER | I2S_MODE_RX);
            cfg.sample_rate = SAMPLE_RATE;
            cfg.bits_per_sample = I2S_BITS_PER_SAMPLE_16BIT;
            cfg.channel_format = I2S_CHANNEL_FMT_ONLY_LEFT;
            cfg.communication_format = I2S_COMM_FORMAT_STAND_I2S;
            cfg.intr_alloc_flags = ESP_INTR_FLAG_LEVEL1;
            cfg.dma_buf_count = 8;
            cfg.dma_buf_len = 64;
            cfg.use_apll = false;

            i2s_pin_config_t pins = {};
            pins.bck_io_num = MIC_SCK;
            pins.ws_io_num = MIC_WS;
            pins.data_in_num = MIC_SD;
            pins.data_out_num = I2S_PIN_NO_CHANGE;

            i2s_driver_install(I2S_PORT_MIC, &cfg, 0, NULL);
            i2s_set_pin(I2S_PORT_MIC, &pins);
            _installed = true;
        }
        recordedSize = 0;
        Serial.println("开始录音...");
    }

    bool recordChunk() {
        if (recordedSize >= bufferSize) return false;

        size_t bytesRead = 0;
        size_t toRead = min((size_t)1024, bufferSize - recordedSize);
        i2s_read(I2S_PORT_MIC, buffer + recordedSize, toRead, &bytesRead, 100);
        recordedSize += bytesRead;
        return true;
    }

    void stopRecording() {
        Serial.println("录音结束: " + String(recordedSize) + " bytes (" +
                       String(recordedSize / (SAMPLE_RATE * 2)) + " 秒)");
        if (_installed) {
            i2s_driver_uninstall(I2S_PORT_MIC);
            _installed = false;
        }
    }

    // 读取一小段音频，返回音量（用于 VAD 检测）
    int readVolume() {
        if (!_installed) {
            startRecording();
        }

        int16_t samples[160];
        size_t bytesRead = 0;
        i2s_read(I2S_PORT_MIC, samples, sizeof(samples), &bytesRead, 50);

        if (bytesRead == 0) return 0;

        int32_t sum = 0;
        int count = bytesRead / 2;
        for (int i = 0; i < count; i++) {
            sum += abs(samples[i]);
        }
        return sum / count;
    }

    // 语音活动检测 + 自动录音
    // 调用前确保已 startRecording()
    // 返回 true 如果录到了有效语音
    bool listenAndRecord() {
        if (!_installed) startRecording();

        int vol = readVolume();

        if (vol < VAD_THRESHOLD) return false;

        // 检测到声音，开始正式录音
        Serial.println("检测到声音 (音量:" + String(vol) + ")，录音中...");
        recordedSize = 0;
        unsigned long speechStart = millis();
        unsigned long lastSoundTime = millis();

        while (true) {
            // 录一段
            size_t bytesRead = 0;
            size_t toRead = min((size_t)1024, bufferSize - recordedSize);
            if (toRead == 0) break;

            i2s_read(I2S_PORT_MIC, buffer + recordedSize, toRead, &bytesRead, 100);
            recordedSize += bytesRead;

            // 检查音量
            if (bytesRead >= 2) {
                int16_t* smp = (int16_t*)(buffer + recordedSize - bytesRead);
                int32_t sum = 0;
                int cnt = bytesRead / 2;
                for (int i = 0; i < cnt; i++) sum += abs(smp[i]);
                int curVol = sum / cnt;

                if (curVol > VAD_THRESHOLD) {
                    lastSoundTime = millis();
                }
            }

            // 静音超过阈值，认为说完了
            if (millis() - lastSoundTime > SILENCE_MS) break;

            // 最长录 RECORD_SECONDS 秒
            if (millis() - speechStart > RECORD_SECONDS * 1000) break;
        }

        unsigned long duration = millis() - speechStart;
        Serial.println("录音: " + String(recordedSize) + " bytes (" + String(duration) + "ms)");

        // 太短的忽略
        if (duration < MIN_SPEECH_MS || recordedSize < 3200) {
            Serial.println("语音太短，忽略");
            return false;
        }

        // 停止麦克风 I2S
        stopRecording();
        return true;
    }

    // 生成带 WAV 头的完整数据
    uint8_t* getWavData(size_t* outLen) {
        size_t wavLen = recordedSize + 44;
        uint8_t* wav = (uint8_t*)ps_malloc(wavLen);
        if (!wav) wav = (uint8_t*)malloc(wavLen);
        if (!wav) { *outLen = 0; return NULL; }

        writeWavHeader(wav, recordedSize);
        memcpy(wav + 44, buffer, recordedSize);
        *outLen = wavLen;
        return wav;
    }

private:
    void writeWavHeader(uint8_t* header, size_t dataSize) {
        uint32_t fileSize = dataSize + 36;
        uint32_t byteRate = SAMPLE_RATE * 1 * 2;  // mono, 16bit
        uint16_t blockAlign = 2;

        memcpy(header, "RIFF", 4);
        memcpy(header + 4, &fileSize, 4);
        memcpy(header + 8, "WAVE", 4);
        memcpy(header + 12, "fmt ", 4);
        uint32_t fmtSize = 16;
        memcpy(header + 16, &fmtSize, 4);
        uint16_t audioFormat = 1;  // PCM
        memcpy(header + 20, &audioFormat, 2);
        uint16_t channels = 1;
        memcpy(header + 22, &channels, 2);
        uint32_t sr = SAMPLE_RATE;
        memcpy(header + 24, &sr, 4);
        memcpy(header + 28, &byteRate, 4);
        memcpy(header + 32, &blockAlign, 2);
        uint16_t bps = 16;
        memcpy(header + 34, &bps, 2);
        memcpy(header + 36, "data", 4);
        memcpy(header + 40, &dataSize, 4);
    }
};

#endif
