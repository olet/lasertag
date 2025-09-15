# 🎯 物理小球交互系统

## 📋 系统概述

这是一个创新的混合现实交互系统，允许玩家使用**任何现实世界物品**拍打虚拟小球。系统通过 Quest 3 的深度感知技术，实现了真实物理交互。

## 🏗️ 架构设计

### 核心组件

```
MultiBallMarkingSystem (管理器)
    ↓ 管理多个
InteractableBall (单球组件)
    ↓ 使用
PhysicalInteractionDetector (检测器)
    ↓ 依赖
EnvironmentMapper (Quest 3 深度API)
```

### 文件结构

```
Assets/Anaglyph/LaserTag/Objects/Gameplay/
├── BallMarkingMethod.cs          # 标记方法枚举
├── InteractableBall.cs           # 可交互小球组件
├── PhysicalInteractionDetector.cs # 物理检测器
└── MultiBallMarkingSystem.cs     # 多球管理器

Assets/Anaglyph/LaserTag/Systems/
└── CodeDrivenSetup.cs            # 系统初始化 (已修改)

Assets/Anaglyph/LaserTag/Objects/Gameplay/
└── BallFactory.cs                # 小球工厂 (已修改)
```

## 🎮 使用方法

### 1. 标记小球 (选择目标)

| 方式 | 操作 | 视觉反馈 |
|------|------|----------|
| 👀 **注视标记** | 看着钉住的小球 | 黄色发光 |
| 🎮 **指向标记** | 左手控制器指向小球 | 青色发光 |
| ⌨️ **手动标记** | 按 M 键标记最近的球 | 紫色发光 |

### 2. 物理拍打 (触发交互)

一旦小球被标记，可以用**任何现实物品**拍打：

- **👋 空手拍打**：直接用手掌拍
- **📚 工具拍打**：书本、尺子、棍子
- **🚶 身体拍打**：用肩膀、胳膊撞
- **👨‍👩‍👧‍👦 他人拍打**：没戴头盔的人帮忙拍
- **🪑 物体推挤**：椅子推过去碰撞

### 3. 系统响应

- 球被拍中后**立即重新激活物理**
- 根据接触方向施加**反向反弹力** (1.5f-3f)
- 球变为**青色**表示被物理拍中
- 自动**取消标记**，停止检测

## ⚡ 性能特性

### 高效检测机制

- **按需检测**：只有标记的球才消耗性能
- **低频检测**：每 0.1 秒检测一次
- **6方向射线**：检测半径仅 10cm
- **智能清理**：自动移除无效标记

### 性能数据

```
最大同时标记球数: 10个
单球检测开销: 6条射线 × 0.1秒间隔 = 极低
总系统开销: 最多 60条射线/秒 = 可忽略
```

## 🔧 技术实现

### 标记系统

```csharp
// 注视标记
Camera.main.ScreenPointToRay() → Physics.Raycast() → MarkBall()

// 指向标记  
OVRInput.GetLocalControllerPosition/Rotation() → Ray → MarkBall()

// 手动标记
FindNearestStuckBall() → MarkBall(Manual)
```

### 物理检测

```csharp
// 6方向检测
Vector3[] directions = { up, down, left, right, forward, back };

foreach(direction in directions) {
    bool hit = EnvironmentMapper.Raycast(ballPos, direction, 0.1f);
    if (hit) TriggerPhysicalHit(-direction);
}
```

### 碰撞响应

```csharp
// 重新激活物理
rigidbody.isKinematic = false;
rigidbody.useGravity = true;

// 施加反向力
Vector3 force = (-contactDirection) * Random.Range(1.5f, 3f);
rigidbody.AddForce(force, ForceMode.Impulse);
```

## 🎯 调试功能

### 调试UI (编辑器模式)

- **标记统计**：显示当前标记球数量
- **目标信息**：显示注视和指向目标
- **性能监控**：显示检测次数统计
- **操作提示**：显示快捷键说明

### 调试可视化

```csharp
// 在 PhysicalInteractionDetector 中
[ContextMenu("Toggle Debug Visualization")]
public void ToggleDebugVisualization()
```

### 控制台输出

```
[MultiBallMarking] 球 Ball_001 被👀 注视标记，当前标记数: 3
[PhysicalDetector] Ball_001 开始物理接触检测，间隔:0.1s
[PhysicalDetector] 检测到从Vector3(1,0,0)方向的物理接触！
[InteractableBall] 球 Ball_001 被物理拍飞！接触方向:(1.00,0.00,0.00), 反弹力:(2.34,0.00,0.00)
```

## 🚀 扩展功能 (预留)

### 高级速度检测

```csharp
struct AdvancedDetectionData {
    Vector3 lastHitPoint;
    Vector3 currentHitPoint;  
    float estimatedVelocity;  // 根据碰撞点距离计算拍打速度
    Vector3 preciseDirection; // 更精确的拍打方向
}
```

### 精确方向检测

- 多点采样获取更准确的碰撞面
- 基于碰撞点移动轨迹计算真实拍打方向
- 根据拍打速度调整反弹力度

## 📊 系统集成

### 与现有系统兼容

- ✅ **激光枪交互**：可以同时使用激光和物理拍打
- ✅ **环境物理**：复用 `EnvironmentBallPhysics` 系统
- ✅ **网络同步**：保持 `NetworkObject` 功能
- ✅ **性能优化**：延续 `isKinematic` 优化

### 自动初始化

```csharp
// BallFactory 自动添加组件
var interactableBall = ballObject.AddComponent<InteractableBall>();

// CodeDrivenSetup 自动创建管理器
var markingSystem = markingSystemObject.AddComponent<MultiBallMarkingSystem>();
```

## 🎮 用户体验

### 直观的交互流程

1. **👀 看着小球** → 小球发黄光
2. **🤚 伸手拍打** → 系统检测接触
3. **💥 球被拍飞** → 变青色，重新激活物理
4. **🎯 重复体验** → 可以标记多个球同时拍打

### 真实物理感受

- **即时响应**：检测延迟 < 0.1s
- **力度反馈**：根据接触方向自然反弹
- **视觉反馈**：清晰的颜色标识系统状态
- **通用兼容**：任何物品都可以作为拍打工具

---

**🌟 这是一个突破性的混合现实交互系统，完美融合了虚拟游戏世界与现实物理交互！**
