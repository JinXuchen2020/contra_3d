# Assets/Scenes/ — 场景说明（T-SYS-001）

## Boot.unity（唯一入口场景 — 已创建）

**状态**: ✅ 已创建并验证

### 场景组成
- **主相机**: Perspective, FOV 60° (对齐 CameraParams.FovDeg)
- **方向光**: URP Directional Light
- **测试几何体**: 1 块地面 Plane + 1 个 Cube
- **准星 UI**: Screen Space Canvas (预留)
- **引导脚本**: GameBootstrap + CameraRigBootstrap 已挂载

### 创建步骤（已完成）
1. ✅ Unity Editor 已安装 (6000.6.0f1)
2. ✅ Boot.unity 场景已创建
3. ✅ 场景组件已配置
4. ✅ BuildScript.cs 已创建用于批量构建

### 验证状态
- **EditMode 测试**: 255 PASS, 7 SKIP, 0 FAIL
- **dotnet build**: 成功
- **Unity 批量构建**: 需要关闭 Unity Editor 后执行

### 依赖约束
- Boot 场景仅使用 URP/Lit 默认资源
- 输入链路走 Input System 包
