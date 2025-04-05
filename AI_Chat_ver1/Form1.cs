namespace AI_Chat_ver1
{

    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json;
    using System.Diagnostics;
    using System.Data;
    using static AI_Chat_ver1.Form1;
    using System.Windows.Forms;
    using System.Drawing.Drawing2D;
    using System.Net.Http;

    using System.Threading.Tasks;

    public partial class Form1 : Form
    {

        static string baseProjectDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));


        static string serverDirectory = Path.Combine(baseProjectDirectory, "Assets", "server");
        static string chatDirectory = Path.Combine(baseProjectDirectory, "Assets", "chats");

        static string fontFamily = "Arial";
        static int fontSizeText = 16;
        static int fontSizeHeader = 18;

        static Padding msgPadding = new Padding(10);

        // ���� ���� ������ (��� ����)
        static Color panelColor = Color.FromArgb(28, 28, 30); // ����� �����-�����, ����� ������

        // ���� ���� ���������
        static Color textBackColor = Color.FromArgb(44, 44, 46); // �����-����� � ������ �������� ������

        // ���� ������
        static Color textColor = Color.FromArgb(235, 235, 235); // ������-�����, ���� ����� ����� ��� ����� �����


        static int borderRadius = 15;
        static int borderWidth = 2;
        static Color borderColor = Color.White;

        private readonly HttpClient _httpClient = new HttpClient(); //�������� ������



        string currentContextPath;
        Process serverProcess;

        string currentModelPath;
        //List<string> chatFilesNames = new List<string>();

        public Form1()
        {
            InitializeComponent();



        }


        //�������� �������� ����� � ��������
        private void load_chat_names()
        {
            if (Directory.Exists(chatDirectory))
            {
                // �������� ��� ����� JSON � �����
                var chatFiles = Directory.GetFiles(chatDirectory, "*.json");

                foreach (var chatFile in chatFiles)
                {
                    // ������ ���������� �����
                    var jsonContent = File.ReadAllText(chatFile);

                    // �������������� JSON � ������ Chat
                    var chat = JsonConvert.DeserializeObject<Chat>(jsonContent);

                    // ���� � ���� ���� ���� �� ���� ���������
                    if (chat.Messages?.Any() == true)
                    {
                        // ������� ������ ��������� �� ������������
                        var firstUserMessage = chat.Messages.FirstOrDefault(m => m.Role == "user")?.Content;

                        if (!string.IsNullOrEmpty(firstUserMessage))
                        {
                            //�������� �������� �����
                            var fileName = Path.GetFileName(chatFile);

                            // ��������� �������� ���� � DataGridView
                            dataGridView1.Rows.Add(firstUserMessage, fileName);

                        }
                    }
                }
            }
            else
            {
                MessageBox.Show($"����� � ������ �� �������: {chatDirectory}");
            }
        }


        //�������� ���������
        private void load_chat_data(string chatPath)
        {
            if (!File.Exists(chatPath))
            {
                MessageBox.Show("��� �� ������.", "������", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var jsonContent = File.ReadAllText(chatPath);
            var chat = JsonConvert.DeserializeObject<Chat>(jsonContent);

            panel2.Controls.Clear();
            //int yPosition = yPos;

            foreach (var message in chat.Messages)
            {
                var roleLabel = new Label
                {
                    Text = message.Role + ":",
                    AutoSize = true,
                    //Location = new Point(10, yPosition),
                    Font = new Font(fontFamily, fontSizeHeader, FontStyle.Bold),
                    Padding = msgPadding,
                    ForeColor = textColor
                };
                panel2.Controls.Add(roleLabel);

                var messageTextBox = new RichTextBox
                {
                    Text = message.Content,
                    Multiline = true,
                    ReadOnly = true,
                    Width = panel2.ClientSize.Width - 40,
                    //Location = new Point(10, yPosition + roleLabel.Height + 5),
                    Font = new Font(fontFamily, fontSizeText),
                    ForeColor = textColor,
                    Height = TextRenderer.MeasureText(message.Content, new Font(fontFamily, fontSizeText), new Size(panel2.ClientSize.Width - 40, 0), TextFormatFlags.WordBreak).Height + 10,
                    BackColor = textBackColor,
                    BorderStyle = BorderStyle.None,
                    Dock = DockStyle.Fill, //������, ���� ��� �������
                    //Padding = msgPadding,
                    //ScrollBars = ScrollBars.None
                };
                messageTextBox.MouseWheel += richTextBox_MouseWheel;

                var textBoxContainer = new Panel
                {
                    Padding = msgPadding,
                    Width = panel2.Width - 25,
                    BackColor = textBackColor,
                    Height = TextRenderer.MeasureText(message.Content, new Font(fontFamily, fontSizeText), new Size(panel2.ClientSize.Width - 40, 0), TextFormatFlags.WordBreak).Height + 10 + 20,

                };
                textBoxContainer.Controls.Add(messageTextBox);

                panel2.Controls.Add(textBoxContainer);
                //panel2.Controls.Add(messageTextBox);

                //yPosition += roleLabel.Height + messageTextBox.Height + 15;

                //������ ���������� �����
                //yPos = yPosition;
            }

            panel2.AutoScroll = true;
            panel2.VerticalScroll.Value = panel2.VerticalScroll.Maximum;
        }

        //�������� �������
        private void Form1_Load(object sender, EventArgs e)
        {
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:8000/");

            dataGridView1.ColumnCount = 2;
            panel2.AutoScroll = true;
            panel2.BackColor = panelColor;


            //������� ������� � ���������� ������
            dataGridView1.Columns[1].Visible = false;


            load_chat_names();



            var fileName = dataGridView1.Rows[0].Cells[1].Value?.ToString();

            // ����� ������ ��� �������� ������ ����
            if (!string.IsNullOrEmpty(fileName))
            {
                string chatFilePath = Path.Combine(chatDirectory, fileName);
                load_chat_data(chatFilePath);
            }

            currentContextPath = Path.Combine(chatDirectory, dataGridView1.Rows[0].Cells[1].Value?.ToString());

            StartServer();

            send_to_server_json((string)currentContextPath);

            var response = _httpClient.PostAsync("connected", null);
            Task.Run(async () =>
            {
                await ping_server();
            });


            //��������
            //send_to_server_json((string)currentContextPath);
            //load_chat_data(currentContextPath);
        }

        private async void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            var response = await _httpClient.PostAsync("kill_server", null);
            //if (serverProcess != null && !serverProcess.HasExited)
            //{
                //serverProcess.Kill();
            //}
        }

        //���������� ������� ��� �������� �����
        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            var response = await _httpClient.PostAsync("kill_server", null);
            //if (serverProcess != null && !serverProcess.HasExited)
            //{
                //serverProcess.Kill();
            //}
        }

        //������ ��� ������ ������
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel) return;
            currentModelPath = openFileDialog1.FileName;
        }


        //���������� ���������
        public class Message
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }


        //������ ��������� (������)
        public class Chat
        {
            public List<Message> Messages { get; set; }
        }


        //��������� �������� ����������
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // ��������� �������������� ���������
            textBox1.ScrollBars = ScrollBars.None;

            // ��������� ������ ������ � ������ ������ TextBox
            int textHeight = TextRenderer.MeasureText(textBox1.Text, textBox1.Font, textBox1.ClientSize, TextFormatFlags.WordBreak).Height;

            // ����������� ������ TextBox
            int minHeight = 103;

            // ������������ ������ TextBox (���������, ����� �� ������� ���� �����)
            int maxHeight = 450;

            // ��������� ����� ������ TextBox
            int newHeight = Math.Max(minHeight, Math.Min(textHeight + 10, maxHeight));  // +10 ��� ���������� �������

            textBox1.ScrollBars = textBox1.Height >= maxHeight ? ScrollBars.Vertical : ScrollBars.None;

            // ������������� ����� ������
            textBox1.Height = newHeight;

            // ���������� ������ �������� ��� TextBox
            //button3.Top = textBox1.Bottom + 5;
        }


        //���� ������
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // ���� Shift �� ������������, ���������� ���������
                if (!e.Shift)
                {
                    e.SuppressKeyPress = true; // ������������� ���������� ����� ������
                    pushMessage(); // ����� ��� �������� ���������
                    textBox1.Clear();
                }
                // ���� Shift ������������, ��������� ����� ������
                else
                {
                    // �� ������ ������, ����� ��������� ���������� ����� ������
                }
            }
        }


        //��������������� ���������� � json
        private void add_to_json(Message msg)
        {
            var jsonContent = File.ReadAllText(currentContextPath);
            var chat = JsonConvert.DeserializeObject<Chat>(jsonContent);

            chat.Messages.Add(msg);

            jsonContent = JsonConvert.SerializeObject(chat, Formatting.Indented);

            // ������ ������������ JSON ������� � ����
            File.WriteAllText(currentContextPath, jsonContent);
        }


        //��������� ��������� ������� �� json`a � ���
        private void add_new_msg(string chatPath) {

            if (!File.Exists(chatPath))
            {
                MessageBox.Show("��� �� ������.", "������", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var jsonContent = File.ReadAllText(chatPath);
            var chat = JsonConvert.DeserializeObject<Chat>(jsonContent);

            var message = chat.Messages.LastOrDefault();

            var roleLabel = new Label
            {
                Text = message.Role + ":",
                AutoSize = true,
                //Location = new Point(10, yPos),
                Padding = msgPadding,
                Font = new Font(fontFamily, fontSizeHeader, FontStyle.Bold),
                ForeColor = textColor
            };
            panel2.Controls.Add(roleLabel);

            var messageTextBox = new RichTextBox
            {
                Text = message.Content,
                Multiline = true,
                ReadOnly = true,
                Width = panel2.ClientSize.Width - 40,
                //Location = new Point(10, yPosition + roleLabel.Height + 5),
                Font = new Font(fontFamily, fontSizeText),
                ForeColor = textColor,
                Height = TextRenderer.MeasureText(message.Content, new Font(fontFamily, fontSizeText), new Size(panel2.ClientSize.Width - 40, 0), TextFormatFlags.WordBreak).Height + 10,
                BackColor = textBackColor,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill, //������, ���� ��� �������
            };
            messageTextBox.MouseWheel += richTextBox_MouseWheel;

            var textBoxContainer = new Panel
            {
                Padding = msgPadding,
                Width = panel2.Width - 25,
                BackColor = textBackColor,
                Height = TextRenderer.MeasureText(message.Content, new Font(fontFamily, fontSizeText), new Size(panel2.ClientSize.Width - 40, 0), TextFormatFlags.WordBreak).Height + 10 + 20,

            };
            textBoxContainer.Controls.Add(messageTextBox);

            panel2.Controls.Add(textBoxContainer);

            panel2.VerticalScroll.Value = panel2.VerticalScroll.Maximum;

            //yPos += roleLabel.Height + messageTextBox.Height + 15;

        }

        //��� ��������� � json � ��������� � ������
        private void pushMessage()
        {
            string messageContent = textBox1.Text;

            if (string.IsNullOrWhiteSpace(messageContent))
            {
                MessageBox.Show("��������� �� ����� ���� ������.");
                return;
            }

            string chatPath = currentContextPath;



            var newMessage = new Message
            {
                Role = "user",
                Content = messageContent
            };

            add_to_json(newMessage);


            //�������� �� ��� ������ � ���
            //load_chat_data(currentContextPath);

            //add_new_msg(currentContextPath);


            //���-�� ��� ���������� �������� �� ������
            sendMessage(messageContent);



            //�������� �� ��� ������ � ���
            //load_chat_data(currentContextPath);

            add_new_msg(currentContextPath);

        }


        //�������� ���������-������� �� ������
        private async void sendMessage(string messageContent)
        {

            var values = new Dictionary<string, string>
            {
                { "content", messageContent }
            };

            var content = new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("send_message", content);

                var responseString = await response.Content.ReadAsStringAsync();

                // �������������� JSON-������ �� �������
                var responseDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);

                // ���������� �������� "response" �� ������������������ �������
                string assistantMessage = responseDict.ContainsKey("response") ? responseDict["response"] : "������ ��� ��������� ������!";

                var newMessage = new Message
                {
                    Role = "assistant",
                    Content = assistantMessage
                };

                add_to_json(newMessage);


            }
            catch (Exception ex)
            {
                var newErrMessage = new Message
                {
                    Role = "assistant",
                    Content = ex.Message//"�� ���� ������������ � �������!"
                };
                add_to_json(newErrMessage);
            }

            //�������� �� ��� ������ � ���
            //load_chat_data(currentContextPath);

            add_new_msg(currentContextPath);

        }

        //�������� ��������� ����
        private async void send_to_server_json(string jsonPath)
        {

            try
            {
                // ������ ����������� JSON �����
                var jsonContent = File.ReadAllText(jsonPath);

                // ���������� ��� ��������
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // �������� POST-������� �� ������
                var response = await _httpClient.PostAsync("send_full_context", content);



            }
            catch (Exception ex)
            {
                //�������� �����-������ ���������
            }

        }

        //����� ����
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // ���������, ��� ���� �� ������ ��������� � �������� �������
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // �������� �������� ����� �� �������� �������
                var fileName = dataGridView1.Rows[e.RowIndex].Cells[1].Value?.ToString();

                if (!string.IsNullOrEmpty(fileName))
                {
                    //�������� ���� � ����, ��������� ������� (��� ��� ���� ��������)
                    string chatFilePath = Path.Combine(chatDirectory, fileName);
                    currentContextPath = chatFilePath;

                    //���������� json � ���������� �� ������, ���������� �� ����� �������
                    send_to_server_json((string)currentContextPath);
                    load_chat_data(chatFilePath);
                }

            }
        }

        //���� ��� ��� ��� ��� � �������� ����
        private void dataGridView1_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                string pathToDelete = Path.Combine(chatDirectory, dataGridView1.Rows[e.RowIndex].Cells[1].Value?.ToString());

                // �������� �������� ���������� ������
                Rectangle cellRectangle = dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                Point cellLocation = dataGridView1.PointToScreen(new Point(cellRectangle.X, cellRectangle.Y));

                Form popupForm = new Form {
                    Size = new System.Drawing.Size(60, 90),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(cellLocation.X + e.X, cellLocation.Y + e.Y),
                    //FormBorderStyle = FormBorderStyle.None, // ������� ������� � ���������
                    //TopMost = true                          // ����� ����� ������ ������ ����
                };

                Button delButton = new Button { 
                    Text = "�������",
                    Dock = DockStyle.Fill,
                };

                delButton.Click += (s, e) =>
                {
                    popupForm.Close();
                    if (File.Exists(pathToDelete))
                    {
                        File.Delete(pathToDelete);
                    }
                    dataGridView1.Rows.Clear();
                    
                    load_chat_names();
                    currentContextPath = Path.Combine(chatDirectory, dataGridView1.Rows[0].Cells[1].Value.ToString());
                    load_chat_data(currentContextPath);
                };

                popupForm.Controls.Add(delButton);

                popupForm.Deactivate += (s, args) =>
                {
                    popupForm.Close();
                };


                //popupForm.StartPosition
                popupForm.ShowDialog(this);

            }
        }

        //��� ���������� ���������, ����� ������ � �������� ������:

        private void richTextBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (panel2.VerticalScroll.Visible)
            {
                int newValue = panel2.VerticalScroll.Value - e.Delta;
                newValue = Math.Max(panel2.VerticalScroll.Minimum, Math.Min(newValue, panel2.VerticalScroll.Maximum - panel2.VerticalScroll.LargeChange + 1));
                panel2.VerticalScroll.Value = newValue;
                panel2.Invalidate(); // �������������� ������
            }
        }

        //�������� ������ ����
        private void button3_Click(object sender, EventArgs e)
        {
            var newChat = new Chat
            {
                Messages = new List<Message> { }
            };

            //��� � ���� ���� ��������
            string newChatFileName = $"chat_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string newChatFilePath = Path.Combine(chatDirectory, newChatFileName);

            //��������� ��������, �������� ��� ������������
            AddChatToDataGridView(newChatFileName, "����� ���");

            //������������+������ � ����
            var jsonContent = JsonConvert.SerializeObject(newChat, Formatting.Indented);
            File.WriteAllText(newChatFilePath, jsonContent);

            currentContextPath = newChatFilePath;

            load_chat_data(newChatFilePath);
        }

        //���������� �������� ������ ���� � ��������
        private void AddChatToDataGridView(string fileName, string firstMessage)
        {
            // ��������� ����� ������ � ������ DataGridView
            dataGridView1.Rows.Insert(0, firstMessage, fileName);

            // �������� ����� ������
            dataGridView1.ClearSelection();
            dataGridView1.Rows[0].Selected = true;
        }


        //����� �������
        private void StartServer()
        {
            string pythonExe = "python"; // ��� ���� �� Python, ���� �� �� �������� � PATH
            string scriptPath = Path.Combine(serverDirectory, "main.py");

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            serverProcess = new Process
            {
                StartInfo = start
            };

            serverProcess.Start();

            // �����������: ������ ������������ ������ � ������
            serverProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            serverProcess.BeginOutputReadLine();

            serverProcess.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
            serverProcess.BeginErrorReadLine();
        }

        //ping �������, �������� ������� ������ �� ������ �����
        private async Task ping_server()
        {
            try {
                while (true) {
                    var response = await _httpClient.PostAsync("ping", null);
                    await Task.Delay(5000);
                }

            }
            catch (Exception ex) { 
                
            }
        }

        //�������� �� ������ ���� � ������ ��� ����������� ��������
        /*private async void button2_Click(object sender, EventArgs e)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    // ������� ������ ��������� � ����� �������
                    var values = new Dictionary<string, string>
                    {
                        { "path", currentModelPath }
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json");

                    // �������� POST-������� �� ������
                    var response = await client.PostAsync("http://127.0.0.1:8000/send_model_path", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // ������ ������ �� �������
                        var responseContent = await response.Content.ReadAsStringAsync();

                        // �������������� JSON-������
                        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);

                        // ������ ����������� �������� � ����������� �� � ������-����
                        set_status_bar(string.Join("; ", responseJson.Values));
                    }
                    else
                    {
                        set_status_bar($"������: {response.StatusCode} - {response.ReasonPhrase}");
                    }


                }
                catch (Exception ex)
                {
                    //�������� �����-������ ���������
                    set_status_bar($"������: {ex.Message}");
                }
            }
        }*/


        //�������� �� ������ ���� � ������ ��� ����������� ��������
        private async void button2_Click(object sender, EventArgs e) {

            try
            {
                var values = new Dictionary<string, string>
                {
                    { "path", currentModelPath }
                };

                var content = new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json");


                var response = await _httpClient.PostAsync("send_model_path", content);  //��� ��� ���� ������, ���� send message ������ model path

                var responseString = await response.Content.ReadAsStringAsync();

                // �������������� JSON-������ �� �������
                var responseDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);

                // ���������� �������� "response" �� ������������������ �������
                string assistantMessage = responseDict["response"];

                set_status_bar(assistantMessage);
            }

            catch (Exception ex) {
                
            }

        }

        //��������� ������� � ����
        private void set_status_bar(string statusMsg) {
            richTextBox1.Text = "";
            richTextBox1.Text = statusMsg;
        }



    }
}
