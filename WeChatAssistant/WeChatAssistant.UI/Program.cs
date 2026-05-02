using System;
using WeChatAssistant.UI;

namespace WeChatAssistant;

/// <summary>
/// 应用程序入口点
/// 初始化并启动WinForm应用程序
/// </summary>
static class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// </summary>
    /// <remarks>
    /// 应用程序启动流程：
    /// 1. 设置高DPI模式以支持高分辨率显示器
    /// 2. 启用视觉样式使控件外观现代化
    /// 3. 设置兼容的文本渲染模式
    /// 4. 创建并运行主窗体
    /// 
    /// STAThread属性说明：
    /// - 标记主线程为单线程单元(STA)模式
    /// - WinForm应用程序必须使用STA模式
    /// - 确保COM组件和剪贴板操作正常工作
    /// </remarks>
    [STAThread]
    static void Main()
    {
        // 设置高DPI模式
        // SystemAware：根据系统DPI设置自动缩放
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        
        // 启用视觉样式
        // 使控件使用Windows主题样式
        Application.EnableVisualStyles();
        
        // 设置兼容的文本渲染默认值
        // false：使用GDI+文本渲染（更美观）
        Application.SetCompatibleTextRenderingDefault(false);
        
        // 运行主窗体
        // Application.Run() 启动消息循环
        // 直到主窗体关闭才返回
        Application.Run(new MainForm());
    }
}
