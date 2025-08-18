using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Windows;

namespace FootStepBoost
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 注册系统会话结束事件
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            // 这里可以执行关键的清理操作
            // 注意：此事件在系统关机或注销时触发，但时间有限
            PerformCleanup();

            // 如果你需要取消关机（不建议），可以设置 e.Cancel = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 执行清理操作
            PerformCleanup();

            // 取消注册事件处理程序
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;

            base.OnExit(e);
        }

        private void PerformCleanup()
        {
            try
            {
                // 在这里执行所有必要的清理操作
                // 例如：关闭音频设备、保存配置等

                // 重要提示：这里的操作应该尽可能快速完成
                // 因为系统关机时可能只给几秒钟的执行时间
            }
            catch (Exception ex)
            {
                // 记录任何清理过程中的错误
                // 但不要抛出异常，以免影响关闭过程
            }
        }
    }
}

