# Live Dashboard API 接口文档

## 概述

Live Dashboard 是一个实时设备活动仪表盘系统，采用三端架构：
- **服务端 (Server)**: Bun + Next.js 后端，负责数据存储和API服务
- **Windows 代理**: C# .NET 9 系统托盘应用，监控前台窗口活动
- **Android 应用**: Kotlin Jetpack Compose 移动应用，监控前台应用和健康数据

**基础URL**: `http://your-server:3000`

**认证方式**: Bearer Token (JWT格式)

**数据格式**: JSON

---

## 认证机制

### Bearer Token 认证

所有需要认证的API端点都使用 Bearer Token 进行身份验证。

**请求头格式**:
```
Authorization: Bearer <token>
```

**Token 配置**:
Token 通过环境变量 `DEVICE_TOKEN_N` 配置，格式为：
```
DEVICE_TOKEN_1=token:device_id:device_name:platform
DEVICE_TOKEN_2=token:device_id:device_name:platform
```

**示例**:
```bash
DEVICE_TOKEN_1=abc123:windows-pc:MyWindowsPC:windows
DEVICE_TOKEN_2=def456:android-phone:MyAndroidPhone:android
```

**支持的平台**:
- `windows`
- `android`
- `macos`

---

## API 端点

### 1. 设备活动上报 (需要认证)

**端点**: `POST /api/report`

**描述**: 设备代理上报当前前台应用活动

**认证**: 需要 Bearer Token

**请求体**:
```json
{
  "app_id": "string",           // 必填，应用标识符
  "window_title": "string",     // 可选，窗口标题（最大256字符）
  "timestamp": "string",        // 可选，ISO 8601格式时间戳
  "extra": {                    // 可选，额外信息
    "battery_percent": 85,      // 电池电量 (0-100)
    "battery_charging": false,  // 是否充电中
    "music": {                  // 音乐信息
      "title": "歌曲标题",
      "artist": "艺术家",
      "app": "音乐应用名"
    }
  }
}
```

**响应**:
```json
{
  "ok": true
}
```

**错误响应**:
- `401 Unauthorized`: Token 无效或缺失
- `400 Invalid JSON`: 请求体格式错误
- `400 app_id required`: 缺少必填字段 app_id

**Windows 代理调用示例**:
```csharp
var payload = new Dictionary<string, object>
{
    ["app_id"] = "chrome.exe",
    ["window_title"] = "GitHub - Google Chrome",
    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
    ["extra"] = new Dictionary<string, object>
    {
        ["battery_percent"] = 85,
        ["battery_charging"] = false
    }
};
```

**Android 应用调用示例**:
```kotlin
val body = JSONObject().apply {
    put("app_id", "com.android.chrome")
    put("window_title", "GitHub")
    put("timestamp", System.currentTimeMillis())
    put("extra", JSONObject().apply {
        put("battery_percent", 85)
        put("battery_charging", false)
    })
}
```

---

### 2. 获取当前设备状态 (公开)

**端点**: `GET /api/current`

**描述**: 获取所有设备的当前状态、最近活动记录和在线观看者数量

**认证**: 无需认证

**响应**:
```json
{
  "devices": [
    {
      "device_id": "windows-pc",
      "device_name": "MyWindowsPC",
      "platform": "windows",
      "app_id": "chrome.exe",
      "app_name": "Google Chrome",
      "display_title": "GitHub - Google Chrome",
      "last_seen_at": "2026-06-15T10:30:00.000Z",
      "is_online": 1,
      "extra": {
        "battery_percent": 85,
        "battery_charging": false,
        "music": {
          "title": "歌曲标题",
          "artist": "艺术家",
          "app": "Spotify"
        }
      }
    }
  ],
  "recent_activities": [
    {
      "id": 12345,
      "device_id": "windows-pc",
      "device_name": "MyWindowsPC",
      "platform": "windows",
      "app_id": "chrome.exe",
      "app_name": "Google Chrome",
      "display_title": "GitHub - Google Chrome",
      "started_at": "2026-06-15T10:25:00.000Z",
      "created_at": "2026-06-15T10:25:00.000Z"
    }
  ],
  "server_time": "2026-06-15T10:30:00.000Z",
  "viewer_count": 5
}
```

**字段说明**:
- `devices`: 所有设备的当前状态数组
- `recent_activities`: 最近20条活动记录
- `server_time`: 服务器当前时间
- `viewer_count`: 当前在线观看者数量（基于IP统计）

**注意**: 
- `window_title` 字段被移除以保护隐私
- `extra` 字段已解析为JSON对象

---

### 3. 获取活动时间线 (公开)

**端点**: `GET /api/timeline`

**描述**: 获取指定日期的活动时间线数据

**认证**: 无需认证

**查询参数**:
- `date` (必填): 日期，格式为 `YYYY-MM-DD`
- `tz` (可选): 时区偏移量（分钟），例如 `-480` 表示 UTC+8
- `device_id` (可选): 设备ID，用于筛选特定设备

**请求示例**:
```
GET /api/timeline?date=2026-06-15&tz=-480
GET /api/timeline?date=2026-06-15&device_id=windows-pc
```

**响应**:
```json
{
  "date": "2026-06-15",
  "segments": [
    {
      "app_name": "Google Chrome",
      "app_id": "chrome.exe",
      "display_title": "GitHub - Google Chrome",
      "started_at": "2026-06-15T02:00:00.000Z",
      "ended_at": "2026-06-15T02:30:00.000Z",
      "duration_minutes": 30,
      "device_id": "windows-pc",
      "device_name": "MyWindowsPC"
    }
  ],
  "summary": {
    "windows-pc": {
      "Google Chrome": 120,
      "Visual Studio Code": 60
    }
  }
}
```

**字段说明**:
- `segments`: 活动时间段数组
  - `duration_minutes`: 活动持续时间（分钟）
  - `ended_at`: 结束时间（可能为null）
- `summary`: 按设备和应用汇总的使用时间（分钟）

**时间线算法**:
- 如果两个连续活动间隔超过2分钟，认为设备离线
- 离线时间段会被截断为1分钟

---

### 4. 健康检查 (公开)

**端点**: `GET /api/health`

**描述**: 服务器健康状态检查

**认证**: 无需认证

**响应**:
```json
{
  "status": "ok",
  "uptime": 3600,
  "timestamp": "2026-06-15T10:30:00.000Z"
}
```

**字段说明**:
- `status`: 服务器状态，固定为 "ok"
- `uptime`: 服务器运行时间（秒）
- `timestamp`: 当前时间戳

---

### 5. 健康数据上报 (需要认证)

**端点**: `POST /api/health-data`

**描述**: 上报健康数据记录

**认证**: 需要 Bearer Token

**请求体**:
```json
{
  "records": [
    {
      "type": "heart_rate",          // 数据类型
      "value": 72,                   // 数值
      "unit": "bpm",                 // 单位
      "timestamp": "2026-06-15T10:30:00.000Z",  // 记录时间
      "end_time": "2026-06-15T10:35:00.000Z"    // 可选，结束时间
    }
  ]
}
```

**支持的数据类型**:
- `heart_rate`: 心率 (bpm)
- `resting_heart_rate`: 静息心率 (bpm)
- `heart_rate_variability`: 心率变异性 (ms)
- `steps`: 步数 (count)
- `distance`: 距离 (m)
- `exercise`: 运动时间 (min)
- `sleep`: 睡眠时间 (min)
- `oxygen_saturation`: 血氧饱和度 (%)
- `body_temperature`: 体温 (°C)
- `respiratory_rate`: 呼吸频率 (bpm)
- `blood_pressure`: 血压 (mmHg)
- `blood_glucose`: 血糖 (mmol/L)
- `weight`: 体重 (kg)
- `height`: 身高 (m)
- `active_calories`: 活动卡路里 (kcal)
- `total_calories`: 总卡路里 (kcal)
- `hydration`: 饮水量 (mL)
- `nutrition`: 营养摄入

**限制**:
- 每次请求最多500条记录
- `unit` 字段最大20字符

**响应**:
```json
{
  "ok": true,
  "inserted": 5
}
```

**错误响应**:
- `401 Unauthorized`: Token 无效
- `400 records array required`: 缺少 records 数组
- `400 Too many records`: 超过500条记录限制

**Android 应用调用示例**:
```kotlin
val records = listOf(
    ReportClient.HealthRecord(
        type = "heart_rate",
        value = 72.0,
        unit = "bpm",
        timestamp = "2026-06-15T10:30:00.000Z"
    )
)
reportClient.reportHealthData(records)
```

---

### 6. 健康数据查询 (公开)

**端点**: `GET /api/health-data`

**描述**: 查询指定日期的健康数据

**认证**: 无需认证

**查询参数**:
- `date` (必填): 日期，格式为 `YYYY-MM-DD`
- `tz` (可选): 时区偏移量（分钟）
- `device_id` (可选): 设备ID

**请求示例**:
```
GET /api/health-data?date=2026-06-15&tz=-480
GET /api/health-data?date=2026-06-15&device_id=android-phone
```

**响应**:
```json
{
  "date": "2026-06-15",
  "records": [
    {
      "device_id": "android-phone",
      "type": "heart_rate",
      "value": 72,
      "unit": "bpm",
      "recorded_at": "2026-06-15T02:30:00.000Z",
      "end_time": ""
    }
  ]
}
```

---

### 7. Health Connect Webhook (需要认证)

**端点**: `POST /api/health-webhook`

**描述**: 接收 Health Connect 格式的健康数据

**认证**: 需要 Bearer Token

**请求体** (Health Connect 格式):
```json
{
  "timestamp": "2026-03-22T07:41:59Z",
  "app_version": "1.0",
  "steps": [
    {
      "count": 3202,
      "start_time": "2026-03-22T06:00:00Z",
      "end_time": "2026-03-22T07:41:59Z"
    }
  ],
  "heart_rate": [
    {
      "bpm": 61,
      "time": "2026-03-22T07:41:59Z"
    }
  ],
  "oxygen_saturation": [
    {
      "percentage": 98.0,
      "time": "2026-03-22T07:41:59Z"
    }
  ],
  "active_calories": [
    {
      "calories": 45.0,
      "start_time": "2026-03-22T06:00:00Z",
      "end_time": "2026-03-22T07:41:59Z"
    }
  ],
  "total_calories": [
    {
      "calories": 1575.75,
      "start_time": "2026-03-22T00:00:00Z",
      "end_time": "2026-03-22T07:41:59Z"
    }
  ],
  "sleep": [
    {
      "duration_minutes": 480,
      "start_time": "2026-03-21T23:00:00Z",
      "end_time": "2026-03-22T07:00:00Z"
    }
  ],
  "weight": [
    {
      "weight": 70.5,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "blood_pressure": [
    {
      "systolic": 120,
      "diastolic": 80,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "blood_glucose": [
    {
      "level": 5.5,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "body_temperature": [
    {
      "temperature": 36.6,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "respiratory_rate": [
    {
      "rate": 16,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "distance": [
    {
      "distance": 2500,
      "start_time": "2026-03-22T06:00:00Z",
      "end_time": "2026-03-22T07:41:59Z"
    }
  ],
  "exercise": [
    {
      "duration_minutes": 30,
      "start_time": "2026-03-22T06:00:00Z",
      "end_time": "2026-03-22T06:30:00Z"
    }
  ],
  "hydration": [
    {
      "volume": 500,
      "start_time": "2026-03-22T07:00:00Z",
      "end_time": "2026-03-22T07:30:00Z"
    }
  ],
  "heart_rate_variability": [
    {
      "ms": 45,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "resting_heart_rate": [
    {
      "bpm": 62,
      "time": "2026-03-22T07:00:00Z"
    }
  ],
  "height": [
    {
      "height": 1.75,
      "time": "2026-03-22T07:00:00Z"
    }
  ]
}
```

**响应**:
```json
{
  "ok": true,
  "inserted": 15,
  "total_parsed": 15
}
```

**限制**:
- 最多2000条记录

---

### 8. 站点配置 (公开)

**端点**: `GET /api/config`

**描述**: 获取站点配置信息

**认证**: 无需认证

**响应**:
```json
{
  "display_name": "我的仪表盘",
  "title": "Live Dashboard",
  "description": "实时设备活动监控",
  "favicon": "/favicon.ico"
}
```

---

## 数据库模式

### activities 表 (活动记录)

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER | 主键，自增 |
| device_id | TEXT | 设备ID |
| device_name | TEXT | 设备名称 |
| platform | TEXT | 平台 (windows/android/macos) |
| app_id | TEXT | 应用标识符 |
| app_name | TEXT | 应用显示名称 |
| window_title | TEXT | 窗口标题（始终为空，保护隐私） |
| display_title | TEXT | 显示标题（隐私处理后） |
| title_hash | TEXT | 标题的HMAC哈希值 |
| time_bucket | INTEGER | 时间桶（用于去重） |
| started_at | TEXT | 开始时间 (ISO 8601) |
| created_at | TEXT | 记录创建时间 |

**索引**:
- `idx_dedup`: 唯一索引 (device_id, app_id, title_hash, time_bucket)
- `idx_activities_device_started`: (device_id, started_at DESC)
- `idx_activities_started`: (started_at DESC)
- `idx_activities_created`: (created_at)

### device_states 表 (设备状态)

| 字段 | 类型 | 说明 |
|------|------|------|
| device_id | TEXT | 主键，设备ID |
| device_name | TEXT | 设备名称 |
| platform | TEXT | 平台 |
| app_id | TEXT | 当前应用ID |
| app_name | TEXT | 当前应用名称 |
| window_title | TEXT | 窗口标题（始终为空） |
| display_title | TEXT | 显示标题 |
| last_seen_at | TEXT | 最后在线时间 |
| is_online | INTEGER | 是否在线 (1/0) |
| extra | TEXT | 额外信息 (JSON) |

### health_records 表 (健康记录)

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER | 主键，自增 |
| device_id | TEXT | 设备ID |
| type | TEXT | 数据类型 |
| value | REAL | 数值 |
| unit | TEXT | 单位 |
| recorded_at | TEXT | 记录时间 |
| end_time | TEXT | 结束时间 |
| created_at | TEXT | 记录创建时间 |

**约束**:
- `UNIQUE(device_id, type, recorded_at, end_time)`: 防止重复记录

---

## 隐私保护机制

### 1. 窗口标题处理
- 原始 `window_title` **永远不会存储**到数据库
- 使用三级隐私系统处理标题：
  - `SHOW`: 显示完整标题
  - `BROWSER`: 浏览器只显示域名
  - `HIDE`: 完全隐藏

### 2. HMAC 哈希
- 使用 `HASH_SECRET` 环境变量作为密钥
- 对标题进行 SHA-256 HMAC 哈希
- 用于去重，但无法反向解密

### 3. NSFW 过滤
- 自动过滤成人内容
- 基于应用ID、域名和关键词

---

## 错误处理

所有错误响应格式：
```json
{
  "error": "错误描述"
}
```

**常见HTTP状态码**:
- `200 OK`: 请求成功
- `400 Bad Request`: 请求参数错误
- `401 Unauthorized`: 认证失败
- `404 Not Found`: 资源不存在
- `500 Internal Server Error`: 服务器内部错误

---

## 客户端实现

### Windows 代理 (C#)

**认证**:
```csharp
_http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
```

**重试机制**:
- 指数退避：5秒、10秒、20秒...最大60秒
- 连续失败5次后暂停300秒

**心跳间隔**: 60秒

### Android 应用 (Kotlin)

**认证**:
```kotlin
Request.Builder()
    .addHeader("Authorization", "Bearer $token")
```

**超时设置**:
- 连接超时：10秒
- 读取超时：10秒
- 写入超时：10秒

**后台任务**: 使用 WorkManager 实现自调度心跳

---

## 部署配置

### 环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `HASH_SECRET` | 是 | HMAC哈希密钥，使用 `openssl rand -hex 32` 生成 |
| `DEVICE_TOKEN_N` | 是 | 设备Token配置，N为数字 |
| `PORT` | 否 | 服务端口，默认3000 |
| `DB_PATH` | 否 | SQLite数据库路径，默认 `./live-dashboard.db` |
| `STATIC_DIR` | 否 | 静态文件目录，默认 `./public` |

### Docker 部署

```yaml
version: '3.8'
services:
  live-dashboard:
    build: .
    ports:
      - "3000:3000"
    environment:
      - HASH_SECRET=your-secret-here
      - DEVICE_TOKEN_1=token1:device1:MyPC:windows
      - DEVICE_TOKEN_2=token2:device2:MyPhone:android
    volumes:
      - ./data:/app/data
```

---

## 示例代码

### 前端 JavaScript 调用

```javascript
// 获取当前状态
const response = await fetch('/api/current');
const data = await response.json();

// 获取时间线
const timeline = await fetch('/api/timeline?date=2026-06-15&tz=-480');
const timelineData = await timeline.json();

// 获取健康数据
const health = await fetch('/api/health-data?date=2026-06-15');
const healthData = await health.json();
```

### cURL 示例

```bash
# 上报活动
curl -X POST http://localhost:3000/api/report \
  -H "Authorization: Bearer your-token" \
  -H "Content-Type: application/json" \
  -d '{
    "app_id": "chrome.exe",
    "window_title": "GitHub",
    "timestamp": "2026-06-15T10:30:00.000Z"
  }'

# 获取当前状态
curl http://localhost:3000/api/current

# 获取时间线
curl "http://localhost:3000/api/timeline?date=2026-06-15&tz=-480"
```

---

## 版本历史

- **v1.0.0**: 初始版本，支持基础活动监控
- **v1.1.0**: 添加健康数据支持
- **v1.2.0**: 添加 Health Connect Webhook
- **v1.3.0**: 添加音乐信息上报

---

## 相关链接

- [项目仓库](https://github.com/your-repo/live-dashboard)
- [Docker Hub](https://hub.docker.com/r/your-repo/live-dashboard)
- [在线演示](https://your-demo-url.com)
