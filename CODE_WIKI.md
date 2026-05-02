# WeChatFerry.Net 代码库文档

## 项目概述

WeChatFerry.Net 是一个基于 .NET 的 WeChatFerry 客户端 SDK，用于构建微信机器人。它提供了与微信进行交互的完整接口，包括发送消息、接收消息、管理联系人、操作群聊等功能。

**项目地址**: [SilkageNet/WeChatFerry.Net](https://github.com/SilkageNet/WeChatFerry.Net)

**支持平台**: Windows (需要安装特定版本的微信客户端)

**框架**: .NET 6.0-windows 和 .NET 8.0-windows

## 前置条件

1. 安装指定版本的微信客户端（当前支持版本由 WCF 版本决定，默认推荐 3.9.12.17）
2. Windows 操作系统

## 安装

```bash
dotnet add package WeChatFerry.Net
```

## 快速入门

```csharp
using WeChatFerry.Net;

using var client = new WCFClient();
client.OnRecvMsg += (s, e) => Console.WriteLine($"[{e.Type}] {e.Sender}: {e.Content}");
if (!await client.Start())
{
    Console.WriteLine("Failed to start the robot.");
    return;
}
client.SendTxt("filehelper", "Hello, World!");
var selfWxid = await client.RPCGetSelfWxidAsync();
Console.WriteLine($"Self wxid: {selfWxid}");
Console.ReadLine();
```

## 目录结构

```
/
├── WeChatFerry.Net/           # 主项目
│   ├── build/                 # 构建相关文件
│   ├── wcf/                   # WCF SDK 版本
│   ├── WCFClient.cs           # 核心客户端类
│   ├── WCFClient.*.cs         # 客户端功能模块
│   ├── SDK.cs                 # SDK 封装
│   ├── WCF.cs                 # Protobuf 生成代码
│   ├── WCF.proto              # Protobuf 定义文件
│   ├── Message.cs             # 消息相关类
│   ├── Contact.cs             # 联系人相关类
│   ├── PendingMessage.cs      # 待发送消息类
│   ├── WeChatRegistry.cs      # 微信注册表操作
│   └── *.csproj               # 项目文件
├── WeChatFerry.Net.Example/   # 示例项目
└── WeChatFerry.Net.Tests/     # 测试项目
```

## 核心架构

### 1. WCFClient - 核心客户端类

`WCFClient` 是整个 SDK 的核心，采用部分类（partial class）的设计，将功能拆分为多个文件，便于维护。

**主要职责**:
- 初始化和启动 SDK
- 与 WeChatFerry 服务器建立连接
- 发送消息和接收消息
- 管理消息队列和频率控制
- 提供各种功能的 API

**核心属性**:

| 属性 | 说明 |
|------|------|
| `OnRecvMsg` | 接收消息时触发的事件 |
| `_contacts` | 联系人缓存字典 |
| `_msgQueue` | 待发送消息队列 |
| `_options` | 客户端配置选项 |
| `_logger` | 日志记录器 |
| `_sdk` | SDK 实例 |

**核心方法**:

| 方法 | 说明 |
|------|------|
| `Start()` | 启动机器人 |
| `Stop()` | 停止机器人 |
| `CallRPC()` | 同步调用 RPC 接口 |
| `CallRPCAsync()` | 异步调用 RPC 接口 |
| `LoopReceive()` | 循环接收消息 |
| `LoopSend()` | 循环发送消息并控制频率 |

### 2. 模块架构

`WCFClient` 分为以下功能模块:

| 文件 | 功能 |
|------|------|
| [WCFClient.cs](file:///workspace/WeChatFerry.Net/WCFClient.cs) | 核心实现（启动、停止、RPC调用等） |
| [WCFClient.Login.cs](file:///workspace/WeChatFerry.Net/WCFClient.Login.cs) | 登录相关功能 |
| [WCFClient.Contact.cs](file:///workspace/WeChatFerry.Net/WCFClient.Contact.cs) | 联系人管理 |
| [WCFClient.SendMsg.cs](file:///workspace/WeChatFerry.Net/WCFClient.SendMsg.cs) | 消息发送功能 |
| [WCFClient.RecvMsg.cs](file:///workspace/WeChatFerry.Net/WCFClient.RecvMsg.cs) | 消息接收设置 |
| [WCFClient.Room.cs](file:///workspace/WeChatFerry.Net/WCFClient.Room.cs) | 群聊管理 |
| [WCFClient.DB.cs](file:///workspace/WeChatFerry.Net/WCFClient.DB.cs) | 数据库查询 |
| [WCFClient.Tools.cs](file:///workspace/WeChatFerry.Net/WCFClient.Tools.cs) | 其他工具方法 |

## 核心类说明

### 1. WCFClientOptions - 客户端配置

```csharp
public class WCFClientOptions
{
    // 端口号（默认 6666），消息推送端口为 port+1
    public int Port { get; set; } = 6666;
    
    // SDK 路径，会自动查找
    public string SDKPath { get; set; } = string.Empty;
    
    // 是否启用调试模式
    public bool Debug { get; set; }
    
    // 是否禁用微信更新（默认 true）
    public bool DisableWeChatUpgrade { get; set; } = true;
    
    // 消息发送间隔（毫秒）
    public int MessageInterval { get; set; } = 2000;
    
    // 每分钟消息发送数量限制
    public int MessageLimitInMinutes { get; set; } = 6;
    
    // 日志记录器
    public ILogger? Logger { get; set; }
    
    // RPC 超时时间（毫秒）
    public int RPCTimeout { get; set; } = 10000;
}
```

### 2. Message - 消息类

```csharp
public class Message(WxMsg raw)
{
    public WxMsg Raw { get; }
    
    // 是否是自己发送的消息
    public bool IsSelf { get; }
    
    // 是否是群消息
    public bool IsGroup { get; }
    
    // 消息 ID
    public ulong ID { get; }
    
    // 消息类型
    public MessageType Type { get; }
    
    // 消息时间
    public DateTime Time { get; }
    
    // 群 ID（如果是群消息）
    public string RoomID { get; }
    
    // 消息内容
    public string Content { get; }
    
    // 发送者
    public string Sender { get; }
    
    // 签名
    public string Sign { get; }
    
    // 缩略图
    public string Thumb { get; }
    
    // 附加内容
    public string Extra { get; }
    
    // 消息 XML
    public string Xml { get; }
}

// 消息类型枚举
public enum MessageType : uint
{
    Pyq = 0,
    Text = 1,
    Image = 3,
    Voice = 34,
    ContactConfirm = 37,
    PossibleFriend = 40,
    BusinessCard = 42,
    Video = 43,
    Emoticon = 47,
    Location = 48,
    FileOrLink = 49,
    Voip = 50,
    WeChatInit = 51,
    VoipNotify = 52,
    VoipInvite = 53,
    MiniVideo = 62,
    WeChatRedPacket = 66,
    File = 495,
    Music = 496,
    Article = 259,
    GroupNote = 101,
    SyncNotice = 9999,
    System = 10000,
    Recall = 10002,
    SogouEmoticon = 1048625,
    Links = 16777265,
    RedPacket = 436207665,
    RedPacketFace = 536936497,
    VideoAccountVideo = 754974769,
    VideoAccountBusinessCard = 771751985,
    RefrenceMessage = 822083633,
    Pat = 922746929,
    VideoAccountLive = 973078577,
    GoodsLink = 974127153,
    VideoAccountLive2 = 975175729,
    MusicLink = 1040187441,
    File2 = 1090519089
}
```

### 3. Contact - 联系人类

```csharp
public class Contact(RpcContact contact)
{
    // 微信 ID
    public string Wxid { get; }
    
    // 微信号
    public string Code { get; }
    
    // 备注
    public string Remark { get; }
    
    // 昵称
    public string Name { get; }
    
    // 国家
    public string Country { get; }
    
    // 省份
    public string Province { get; }
    
    // 城市
    public string City { get; }
    
    // 性别
    public ContactGender Gender { get; }
    
    // 联系人类型
    public ContactType ContactType { get; }
}

// 联系人性别
public enum ContactGender : int
{
    Unknown = 0,
    Male = 1,
    Female = 2
}

// 联系人类型
public enum ContactType
{
    Unknown = 0,
    Friend = 1,
    Group = 2,
    Public = 3,
    OpenIM = 4,
    Special = 5,
}
```

### 4. PendingMessage - 待发送消息类

```csharp
public class PendingMessage
{
    public enum MessageType
    {
        Txt, Img, File, Emotion, RichTxt, PatMsg, Forward, Xml
    }
    
    // 工厂方法创建各种类型的待发送消息
    public static PendingMessage CreateTxt(string receiver, string content, List<string>? aters = null);
    public static PendingMessage CreateImg(string receiver, string content);
    public static PendingMessage CreateFile(string receiver, string content);
    public static PendingMessage CreateEmotion(string receiver, string content);
    public static PendingMessage CreateRichText(string receiver, RichText richText);
    public static PendingMessage CreatePatMsg(string receiver, string patWxid);
    public static PendingMessage CreateForward(string receiver, ulong forwardMsgID);
    public static PendingMessage CreateXml(string receiver, string path, string content, ulong type);
}
```

### 5. SDK - SDK 封装类

```csharp
public class SDK
{
    // 初始化 SDK
    public delegate int WxInitDelegate(bool debug, int port);
    
    // 销毁 SDK
    public delegate int WxDestroyDelegate();
    
    // 初始化
    public WxInitDelegate WxInit;
    
    // 销毁
    public WxDestroyDelegate WxDestroy;
    
    public SDK(string sdkPath);
}
```

## 功能详细说明

### 1. 登录管理

| 方法 | 说明 |
|------|------|
| `Start(timeout)` | 启动机器人并等待登录 |
| `Stop()` | 停止机器人 |
| `RPCIsLogin()` / `RPCIsLoginAsync()` | 检查是否已登录 |
| `RPCRefreshQrCode()` / `RPCRefreshQrCodeAsync()` | 刷新登录二维码 |
| `RPCGetSelfWxid()` / `RPCGetSelfWxidAsync()` | 获取自己的微信 ID |
| `RPCGetUserInfo()` / `RPCGetUserInfoAsync()` | 获取用户信息 |

### 2. 消息发送

| 方法 | 说明 |
|------|------|
| `SendTxt(receiver, content, aters)` | 发送文本消息（带队列，控制频率） |
| `SendImg(receiver, path)` | 发送图片（带队列） |
| `SendFile(receiver, path)` | 发送文件（带队列） |
| `SendEmotion(receiver, path)` | 发送表情（带队列） |
| `SendRichTxt(receiver, richText)` | 发送富文本/卡片消息（带队列） |
| `SendPatMsg(receiver, patWxid)` | 发送拍一拍（带队列） |
| `SendForward(receiver, msgID)` | 转发消息（带队列） |
| `SendXml(receiver, path, content, type)` | 发送 XML 消息（带队列） |
| `RPCSendTxt()` | 直接发送文本消息（不带队列） |
| `RPCSendImg()` | 直接发送图片（不带队列） |
| ... | 其他直接发送方法 |

### 3. 消息接收

| 方法 | 说明 |
|------|------|
| `RPCEnableRecvTxt(enablePyq)` | 启用接收消息 |
| `RPCDisableRecvTxt()` | 禁用接收消息 |
| `OnRecvMsg` | 接收消息的事件 |

### 4. 联系人管理

| 方法 | 说明 |
|------|------|
| `GetContact(wxid)` | 从缓存获取联系人 |
| `GetContacts()` | 获取所有缓存的联系人 |
| `RefreshContacts()` / `RefreshContactsAsync()` | 刷新联系人列表 |
| `RPCGetContacts()` / `RPCGetContactsAsync()` | 获取所有联系人 |
| `RPCGetContactInfo(wxid)` / `RPCGetContactInfoAsync()` | 获取指定联系人信息 |
| `AcceptFriend(content)` / `AcceptFriendAsync()` | 通过好友申请 |
| `RPCAcceptFriend(v3, v4, scene)` / 异步版 | 通过好友申请（直接调用） |

### 5. 群聊管理

| 方法 | 说明 |
|------|------|
| `RPCAddRoomMembers(roomID, wxids)` / 异步版 | 添加群成员 |
| `RPCDelRoomMembers(roomID, wxids)` / 异步版 | 删除群成员 |
| `RPCInvRoomMembers(roomID, wxids)` / 异步版 | 邀请群成员 |

### 6. 数据库操作

| 方法 | 说明 |
|------|------|
| `RPCGetDBNames()` / 异步版 | 获取数据库名称列表 |
| `RPCGetDBTables(db)` / 异步版 | 获取指定数据库的表列表 |
| `RPCExecDBQuery(db, sql)` / 异步版 | 执行数据库查询，返回 DataTable |
| `RPCExecDBQueryOutputDict(db, sql)` / 异步版 | 执行数据库查询，返回字典列表 |

### 7. 其他工具方法

| 方法 | 说明 |
|------|------|
| `RPCRevokeMsg(id)` / 异步版 | 撤回消息 |
| `RPCGetMsgTypes()` / 异步版 | 获取消息类型列表 |
| `RPCRefreshPyq(id)` / 异步版 | 刷新朋友圈 |
| `RPCRecvTransfer(wxid, tfid, taid)` / 异步版 | 接收转账 |
| `RPCExecOCR(path)` / 异步版 | 执行 OCR 文字识别 |
| `RPCDecryptImage(src, dst)` / 异步版 | 解密图片 |

## 协议通讯机制

项目使用 Protocol Buffers 定义通讯协议，消息格式在 [WCF.proto](file:///workspace/WeChatFerry.Net/WCF.proto) 中定义。

### 通讯架构

1. **服务发现和连接**: SDK 加载并初始化 WeChatFerry 库，启动两个 TCP 服务
   - 命令服务：端口 `_options.Port` (默认 6666)，用于 RPC 调用
   - 消息推送服务：端口 `_options.Port + 1`，用于推送接收到的消息

2. **RPC 调用流程**:
   - 构建 `Request` 消息
   - 通过 Pair Socket 发送到命令服务端口
   - 接收 `Response` 响应消息
   - 解析响应并返回结果

3. **消息推送流程**:
   - 建立与消息推送端口的连接
   - 持续接收推送的消息
   - 解析消息触发 `OnRecvMsg` 事件

### Request 结构

```protobuf
message Request
{
    Functions func = 1;
    
    // 以下字段为可选的参数，根据 func 选择合适的字段
    oneof msg
    {
        Empty empty = 2;
        string str = 3;
        TextMsg txt = 4;
        PathMsg file = 5;
        DbQuery query = 6;
        Verification v = 7;
        MemberMgmt m = 8;
        XmlMsg xml = 9;
        DecPath dec = 10;
        Transfer tf = 11;
        uint64 ui64 = 12;
        bool flag = 13;
        AttachMsg att = 14;
        AudioMsg am = 15;
        RichText rt = 16;
        PatMsg pm = 17;
        ForwardMsg fm = 18;
    }
}
```

### Functions 枚举

```protobuf
enum Functions
{
    FUNC_RESERVED = 0;
    FUNC_IS_LOGIN = 1;
    FUNC_GET_SELF_WXID = 16;
    FUNC_GET_MSG_TYPES = 17;
    FUNC_GET_CONTACTS = 18;
    FUNC_GET_DB_NAMES = 19;
    FUNC_GET_DB_TABLES = 20;
    FUNC_GET_USER_INFO = 21;
    FUNC_GET_AUDIO_MSG = 22;
    FUNC_SEND_TXT = 32;
    FUNC_SEND_IMG = 33;
    FUNC_SEND_FILE = 34;
    FUNC_SEND_XML = 35;
    FUNC_SEND_EMOTION = 36;
    FUNC_SEND_RICH_TXT = 37;
    FUNC_SEND_PAT_MSG = 38;
    FUNC_FORWARD_MSG = 39;
    FUNC_ENABLE_RECV_TXT = 48;
    FUNC_DISABLE_RECV_TXT = 64;
    FUNC_EXEC_DB_QUERY = 80;
    FUNC_ACCEPT_FRIEND = 81;
    FUNC_RECV_TRANSFER = 82;
    FUNC_REFRESH_PYQ = 83;
    FUNC_DOWNLOAD_ATTACH = 84;
    FUNC_GET_CONTACT_INFO = 85;
    FUNC_REVOKE_MSG = 86;
    FUNC_REFRESH_QRCODE = 87;
    FUNC_DECRYPT_IMAGE = 96;
    FUNC_EXEC_OCR = 97;
    FUNC_ADD_ROOM_MEMBERS = 112;
    FUNC_DEL_ROOM_MEMBERS = 113;
    FUNC_INV_ROOM_MEMBERS = 114;
}
```

## 消息控制机制

项目内置了消息频率控制机制，防止发送消息过快被封号：

1. **队列机制**: 所有调用 `Send*` 方法的消息会先进入 `_msgQueue` 队列

2. **发送频率控制**:
   - `MessageInterval`: 消息发送间隔（毫秒，默认 2000ms）
   - `MessageLimitInMinutes`: 每分钟最大消息数（默认 6条）
   - `MessageIntervalRandom`: 随机间隔，增加不确定性

3. **发送流程**:
   - `LoopSend` 任务循环检查队列
   - 检查是否超过频率限制
   - 符合条件则调用对应的 `RPCSend*` 方法发送
   - 记录发送时间

## 示例代码

### 简单的自动回复机器人

```csharp
using WeChatFerry.Net;

using var client = new WCFClient();

client.OnRecvMsg += async (s, msg) =>
{
    if (msg.IsSelf) return;
    
    Console.WriteLine($"收到消息: [{msg.Type}] {msg.Sender}: {msg.Content}");
    
    if (msg.Type == MessageType.Text && !msg.IsSelf)
    {
        var reply = $"收到你的消息: {msg.Content}";
        client.SendTxt(msg.Sender, reply);
    }
};

if (!await client.Start())
{
    Console.WriteLine("启动失败！");
    return;
}

Console.WriteLine("机器人已启动，按任意键退出...");
Console.ReadKey();
```

### 下载文件或图片

```csharp
client.OnRecvMsg += async (s, msg) =>
{
    if (msg.Type == MessageType.FileOrLink || msg.Type == MessageType.Image)
    {
        var extra = string.IsNullOrEmpty(msg.Extra) ? $"{msg.ID}.tmp" : msg.Extra;
        var thumb = string.IsNullOrEmpty(msg.Thumb) ? $"{msg.ID}.thumb.tmp" : msg.Thumb;
        
        var ok = await client.RPCDonwloadAttachAsync(msg.ID, thumb, extra);
        if (ok)
        {
            Console.WriteLine($"文件已下载: {extra}");
        }
    }
};
```

### 使用自定义配置和日志

```csharp
var options = new WCFClientOptions
{
    Port = 7777,
    MessageInterval = 3000,
    MessageLimitInMinutes = 10,
    Logger = new MyCustomLogger() // 实现 ILogger 接口
};

using var client = new WCFClient(options);
```

## 依赖关系

项目主要依赖：

| 依赖 | 用途 |
|------|------|
| `Google.Protobuf` | 处理 Protocol Buffers 序列化 |
| `NanomsgNG.NET` | 提供 Nanomsg Pair Socket 通讯 |

## 注意事项与限制

1. **仅用于学习交流**: 本项目禁止用于商业用途
2. **微信版本匹配**: 必须使用与 WCF SDK 兼容的微信版本
3. **消息频率控制**: 建议保留默认的频率控制，防止账号被封
4. **Windows 平台**: 仅支持 Windows 平台，依赖微信客户端
5. **避免频繁操作**: 不要进行过于频繁的操作，遵守微信使用规范

## 故障排查

### SDK 找不到

确保：
1. 微信版本匹配
2. wcf 目录下有对应的 SDK 文件

### 启动失败

检查：
1. 微信是否已登录
2. 端口是否被占用
3. 日志输出的错误信息

### 收不到消息

检查：
1. 是否成功调用了 `RPCEnableRecvTxt()`
2. 日志是否有错误

## 贡献

欢迎参与项目贡献，提交 Issue 和 PR！

---
**文档版本**: 1.0
**最后更新**: 2025
