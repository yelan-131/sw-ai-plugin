using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SolidWorks.Interop.sldworks;
using SwComAddin.Helpers;
using SwComAddin.Models;
using SwComAddin.Services;
namespace SwComAddin.Views
{
    public partial class MainTaskPaneView : UserControl
    {
        // === Tab 4: AI Assistant ===

        private void AddWelcomeMessage()
        {
            ChatList.Items.Add(new ChatMsg
            {
                Text = "SW AI 助手已就绪。输入中文描述即可建模。\n例如: 创建M10螺栓",
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(232, 238, 254)),
                ForegroundBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            });
        }

        private void AiInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AiSendBtn_Click(sender, e);
        }

        private async void AiSendBtn_Click(object sender, RoutedEventArgs e)
        {
            var message = AiInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            ChatList.Items.Add(new ChatMsg
            {
                Text = message,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                ForegroundBrush = new SolidColorBrush(Colors.White)
            });
            AiInput.Text = "";
            AiSendBtn.IsEnabled = false;

            try
            {
                var body = JsonSerializer.Serialize(new { message = message });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var reply = doc.RootElement.GetProperty("reply").GetString() ?? "无回复";

                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = reply,
                        BackgroundBrush = new SolidColorBrush(Colors.White),
                        ForegroundBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33))
                    });
                }
                else
                {
                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = $"服务错误 ({(int)response.StatusCode})",
                        BackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                        ForegroundBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                    });
                }
            }
            catch (Exception ex)
            {
                ChatList.Items.Add(new ChatMsg
                {
                    Text = $"连接失败: {ex.Message}",
                    BackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                    ForegroundBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                });
            }

            ChatScroll.ScrollToEnd();
            AiSendBtn.IsEnabled = true;
        }
    }
}
