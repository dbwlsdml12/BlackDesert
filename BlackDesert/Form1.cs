using Discord;
using Discord.WebSocket;
using Google.GenAI;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace BlackDesert
{
    public partial class Form1 : Form
    {

        private DiscordSocketClient _client;                // 디스코드 클라이언트
        private ulong guildId = 1467513651983548736;        // 아띠 길드 ID
        private List<User> userList;                        // 플레이어 정보 리스트 (ID, 이름, 사당 완료 여부, 아토 완료 여부)
        private System.Timers.Timer timer;                  // 타이머 (목,토요일마다 자정 체크용)
        private DateTime lastRun = DateTime.MinValue;       // 마지막으로 타이머가 실행된 날짜 (자정 체크용)
        private DateTime lastNotify = DateTime.MinValue;    // 마지막으로 알림을 보낸 날짜 (중복 방지용)
        private NotifyIcon trayIcon;                        // 트레이 아이콘
        private ContextMenuStrip trayMenu;                  // 트레이 메뉴
        public Form1()
        {
            InitializeComponent();
        }

        // 폼 로드 이벤트 (폼 생성자)
        private void Form1_Load(object sender, EventArgs e)
        {
            // 🔴 메뉴 생성
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("열기", null, (s, ev) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            });

            trayMenu.Items.Add("종료", null, (s, ev) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            // 🔴 트레이 아이콘 생성
            trayIcon = new NotifyIcon()
            {
                Icon = new Icon("discord_bot_icon.ico"), // 아이콘 (원하면 바꿔도 됨)
                ContextMenuStrip = trayMenu,
                Visible = true,
                Text = "디스코드 봇 실행중"
            };

            // 🔴 더블클릭 시 창 열기
            trayIcon.DoubleClick += (s, ev) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            };
        }

        //  로그 출력
        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        //  메시지 수신
        private async Task MessageReceivedAsync(SocketMessage message)
        {
            var guild = _client.GetGuild(guildId);

            // 봇의 메시지는 무시
            if (message.Author.IsBot) return;

            // 사당_아토 채널에서 "!사당" 또는 "!아토" 명령어 체크
            if (message.Channel.Name == "사당_아토")
            {
                if (message.Content == "!사당")
                    await message.Channel.SendMessageAsync(check_Black("!사당"));

                if (message.Content == "!아토")
                    await message.Channel.SendMessageAsync(check_Black("!아토"));

            }


            // 검은사당 채널에서 멘션된 사용자 체크 && 검은사당 채널에서 멘션된 이용자 유무체크        
            if (message.Channel.Name == "검은사당" )
            {                                   
                var users = message.Author as SocketGuildUser;

                // 유저 정보가 null인 경우 처리 (예외 방지)
                if (users == null) return;

                if (!users.Roles.Any(r => r.Name == "참모"))
                {
                    await message.Channel.SendMessageAsync("이 채널에서 멘션 또는 명령어를 사용하려면 참모 역할이 필요합니다.");
                    return; // 아무 반응 안하게
                }

                if (message.Attachments.Any())
                {
                    await SaveDiscordImageAsync(message);
                    // 1. 아까 만든 AI 함수 호출해서 닉네임 문자열 받아오기
                    string aiResult = await GetGeminiNicknamesAsync();

                    // 2. 결과가 있으면 콤마로 잘라서 블랙리스트 처리
                    if (!string.IsNullOrEmpty(aiResult) && aiResult != "ERROR_RETRY")
                    {
                        string[] nicknames = aiResult.Split(',');
                        string temp = "";
                        foreach (var n in nicknames)
                        {
                            var target = userList.FirstOrDefault(x => x.Name == n.Trim());
                            if (target != null)
                            {
                                temp+= target.Name + ","; // 처리된 닉네임 누적
                                target.BLACK = true;
                            }
                        }
                        await message.Channel.SendMessageAsync($"{temp}을 모두 완료 처리 햇습니다 .");
                    }                   
                }



                if(message.MentionedUsers.Any())
                {
                    foreach (var u in message.MentionedUsers)
                    {
                        var user = userList.FirstOrDefault(x => x.Id == u.Id);
                        if (user != null)
                            user.BLACK = true;
                    }
                }
                
            }

            // 아토락시온 채널에서 멘션된 사용자 체크 && 아토락시온 채널에서 멘션된 이용자 유무체크
            if (message.Channel.Name == "아토락시온" )
            {
                var users = message.Author as SocketGuildUser;

                // 유저 정보가 null인 경우 처리 (예외 방지)
                if (users == null) return;

                if (!users.Roles.Any(r => r.Name == "참모"))
                {
                    await message.Channel.SendMessageAsync("이 채널에서 멘션 또는 명령어를 사용하려면 참모 역할이 필요합니다.");
                    return; // 아무 반응 안하게
                }

                if (message.Attachments.Any())
                {
                    await SaveDiscordImageAsync(message);
                    // 1. 아까 만든 AI 함수 호출해서 닉네임 문자열 받아오기
                    string aiResult = await GetGeminiNicknamesAsync();

                    // 2. 결과가 있으면 콤마로 잘라서 블랙리스트 처리
                    if (!string.IsNullOrEmpty(aiResult) && aiResult != "ERROR_RETRY")
                    {
                        string[] nicknames = aiResult.Split(',');
                        string temp = "";
                        foreach (var n in nicknames)
                        {
                            var target = userList.FirstOrDefault(x => x.Name == n.Trim());
                            if (target != null)
                            {
                                temp += target.Name + ","; // 처리된 닉네임 누적
                                target.ATO = true;
                            }
                        }
                        await message.Channel.SendMessageAsync($"{temp}을 모두 완료 처리 햇습니다 .");
                    }
                }

                if (message.MentionedUsers.Any())
                {
                    foreach (var u in message.MentionedUsers)
                    {
                        var user = userList.FirstOrDefault(x => x.Id == u.Id);
                        if (user != null)
                            user.ATO = true;
                    }
                }
            }

        }

        //  봇 실행 버튼 클릭 이벤트
        private async void BOT_ON(object sender, EventArgs e)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds
                               | GatewayIntents.GuildMembers
                               | GatewayIntents.GuildMessages
                               | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);

            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.GuildMemberUpdated += OnUserUpdated;

            string token = "MTA4NjM3NzI4MjQ3NTY2NzUzNg.GgOpse.9ULhqb1k40fkyNFZUBJY7goaY4LxMjU_m75olw";

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            MessageBox.Show("봇 실행됨");
            _client.Ready += async () =>
            {
                var guild = _client.GetGuild(guildId);
                await guild.DownloadUsersAsync();
                await Task.Delay(2000);

                userList = guild.Users
                 .Where(u => !u.IsBot && u.Roles.Any(r => r.Name == "일반 길드원"))
                 .Select(u => new User
                 {
                     Id = u.Id,
                     Name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName,
                     ATO = false,
                     BLACK = false,
                     GuildUser= u
                 }).ToList();
                await LoadData();
            };
            StartTimer();

        }

        // 검은사당, 아토락시온 안간사람들 체크
        private string check_Black(string BLack_Ato)
        {
            string blacknotdone = ("```");
            foreach (var user in userList.Where(x => !x.BLACK).Select(x => x.Name).ToList())
            {
                blacknotdone += user + "\n";
            }
            blacknotdone += ("```");
            string atonotdone = ("```\n");
            foreach (var user in userList.Where(x => !x.ATO).Select(x => x.Name).ToList())
            {
                atonotdone += user + "\n";
            }
            atonotdone += ("```");

            if (BLack_Ato == "!사당")
                return blacknotdone;

            if (BLack_Ato == "!아토")
                return atonotdone;
            return "체크오류";
        }

        // 타이머 시작 (목요일, 월요일 자정 체크용)
        private void StartTimer()
        {
           
            timer = new System.Timers.Timer(1000); // 1초마다
            timer.Elapsed += async (s, e) =>
            {
                var now = DateTime.Now;
                var guild = _client.GetGuild(guildId);

                // 🔴 자정에 딱 한 번만 실행
                if (now.Hour == 0 && now.Minute == 0 && lastRun.Date != now.Date)
                {
                    lastRun = now;

                    

                    // 🔥 검은사당 초기화 (일요일)
                    if (now.DayOfWeek == DayOfWeek.Sunday)
                    {
                        var channel = guild.TextChannels.FirstOrDefault(c => c.Name == "검은사당");
                        if (channel != null)
                        {
                            var categoryId = channel.CategoryId;
                            var position = channel.Position;

                            await channel.DeleteAsync();

                            var newChannel = await guild.CreateTextChannelAsync("검은사당", prop =>
                            {
                                prop.CategoryId = categoryId;
                            });

                            await newChannel.ModifyAsync(p => p.Position = position);
                        }

                        foreach (var user in userList)
                            user.BLACK = false;
                    }

                    // 🔥 아토락시온 초기화 (목요일)
                    if (now.DayOfWeek == DayOfWeek.Thursday)
                    {
                        var channel = guild.TextChannels.FirstOrDefault(c => c.Name == "아토락시온");
                        if (channel != null)
                        {
                            var categoryId = channel.CategoryId;
                            var position = channel.Position;

                            await channel.DeleteAsync();

                            var newChannel = await guild.CreateTextChannelAsync("아토락시온", prop =>
                            {
                                prop.CategoryId = categoryId;
                            });

                            await newChannel.ModifyAsync(p => p.Position = position);
                        }

                        foreach (var user in userList)
                            user.ATO = false;
                    }
                }



                //var notifyChannel = guild.TextChannels.FirstOrDefault(c => c.Name == "테스트");
                //if (notifyChannel == null) return;

                //// 🔥 중복 방지
                //if (lastNotify.Minute == now.Minute && lastNotify.Hour == now.Hour && lastNotify.Date == now.Date)
                //    return;

                // 🔥 토요일 13시 (사당 + 아토)
                //if (now.DayOfWeek == DayOfWeek.Saturday && now.Hour == 13 && now.Minute == 0)
                //{
                //    lastNotify = now;

                //    var blackNotDone = userList.Where(x => !x.BLACK).ToList();
                //    var atoNotDone = userList.Where(x => !x.ATO).ToList();

                //    string blackMentions = string.Join(" ", blackNotDone.Select(u => $"<@{u.Id}>"));
                //    string atoMentions = string.Join(" ", atoNotDone.Select(u => $"<@{u.Id}>"));

                //    string msg =
                //    $@"📢 **주간 체크 알림**

                    
                //    [검은사당 미완료]
                //    {(string.IsNullOrEmpty(blackMentions) ? "없음" : blackMentions)}
                    
                //    [아토락시온 미완료]
                //    {(string.IsNullOrEmpty(atoMentions) ? "없음" : atoMentions)}
                //    ";

                //    await notifyChannel.SendMessageAsync(msg);
                //}

                // 🔥 수요일 20시 (아토만)
                //if (now.DayOfWeek == DayOfWeek.Wednesday && now.Hour == 20 && now.Minute == 0)
                //{
                //    lastNotify = now;

                //    var atoNotDone = userList.Where(x => !x.ATO).ToList();
                //    string atoMentions = string.Join(" ", atoNotDone.Select(u => $"<@{u.Id}>"));

                //    string msg =
                //    $@"📢 **아토락시온 알림**


                //    [아토락시온 미완료]
                //    {(string.IsNullOrEmpty(atoMentions) ? "없음" : atoMentions)}";

                //    await notifyChannel.SendMessageAsync(msg);
                //}
            };
            timer.Start();
        }

        // 폼 종료 이벤트 (txt 파일로 데이터 저장)
        private async Task SaveData()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                // 🔴 검은사당 완료자
                var blackDone = userList
                    .Where(x => x.BLACK)
                    .Select(x => x.Name)
                    .ToList();

                // 🔴 아토 완료자
                var atoDone = userList
                    .Where(x => x.ATO)
                    .Select(x => x.Name)
                    .ToList();

                // 🔴 파일 경로
                string blackPath = Path.Combine(basePath, "사당.txt");
                string atoPath = Path.Combine(basePath, "아토.txt");

                // 🔴 파일 저장
                await File.WriteAllLinesAsync(blackPath, blackDone);
                await File.WriteAllLinesAsync(atoPath, atoDone);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 디스코드 봇 시작시 사당,아토 txt 파일에서 데이터 불러오기
        private async Task LoadData()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                string blackPath = Path.Combine(basePath, "사당.txt");
                string atoPath = Path.Combine(basePath, "아토.txt");

                // 🔴 검은사당 불러오기
                if (File.Exists(blackPath))
                {
                    string allText = await File.ReadAllTextAsync(blackPath, System.Text.Encoding.UTF8);

                    // 2. [핵심] 파일 맨 앞의 BOM 기호(65279)를 강제로 제거
                    allText = allText.Trim('\uFEFF', '\u200B');

                    var names = allText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var name in names)
                    {
                        string cleanName = name.Trim(); // 앞뒤 공백 제거

                        // 4. 대소문자나 미세한 공백 차이 무시하고 찾기
                        var user = userList.FirstOrDefault(x => x.Name.Trim().Equals(cleanName, StringComparison.OrdinalIgnoreCase));

                        if (user != null)
                        {
                            user.BLACK = true;
                        }
                    }
                    MessageBox.Show("사당 데이터 불러오기 완료");
                }

                // 🔴 아토 불러오기
                if (File.Exists(atoPath))
                {
                    var lines = await File.ReadAllLinesAsync(atoPath);

                    foreach (var name in lines)
                    {
                        var user = userList.FirstOrDefault(x => x.Name == name);
                        if (user != null)
                            user.ATO = true;
                    }
                    MessageBox.Show("아토 데이터 불러오기 완료");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // 창 최소화 시 트레이로 이동
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide(); // 👉 창 숨김 (트레이로 이동)
            }
        }

        // 디스코드 호스팅 종료
        private async Task StopBot()
        {
            try
            {
                if (_client != null)
                {
                    await _client.StopAsync();   // 봇 정지
                    await _client.LogoutAsync(); // 로그아웃
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        // 폼 종료 이벤트 (데이터 저장 후 봇 종료)
        private async void BOT_OFF(object sender, EventArgs e)
        {
            timer.Stop();           // 타이머 정지
            await SaveData();       // 데이터 저장
            await StopBot();        // 봇 종료         
            Application.Exit();     // 애플리케이션 종료
        }

        // 새로운 유저가 길드에 가입했을 때 처리 (가입 시점에 사당&아토 완료 여부 체크)
        private Task OnUserJoined(SocketGuildUser user)
        {
            if (user.IsBot) return Task.CompletedTask;

            if (!user.Roles.Any(r => r.Name == "일반 길드원"))
                return Task.CompletedTask;

            userList.Add(new User
            {
                Id = user.Id,
                Name = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName,
                ATO = false,
                BLACK = false
            });

            return Task.CompletedTask;
        }
        // 유저가 길드를 떠났을 때 처리 (떠난 유저 정보 리스트에서 제거)
        private Task OnUserLeft(SocketGuild guild, SocketUser user)
        {
            var target = userList.FirstOrDefault(x => x.Id == user.Id);
            if (target != null)
                userList.Remove(target);

            return Task.CompletedTask;
        }
        // 유저의 역할이 변경되었을 때 처리 (일반 길드원 역할 추가/제거 시 정보 리스트 업데이트)
        private async Task OnUserUpdated(Cacheable<SocketGuildUser, ulong> beforeCache, SocketGuildUser after)
        {
            var before = await beforeCache.GetOrDownloadAsync();

            if (before == null) return;

            bool hadRole = before.Roles.Any(r => r.Name == "일반 길드원");
            bool hasRole = after.Roles.Any(r => r.Name == "일반 길드원");

            var user = userList.FirstOrDefault(x => x.Id == after.Id);

            if (!hadRole && hasRole)
            {
                if (user == null)
                {
                    userList.Add(new User
                    {
                        Id = after.Id,
                        Name = string.IsNullOrEmpty(after.DisplayName) ? after.Username : after.DisplayName,
                        ATO = false,
                        BLACK = false
                    });
                }
            }

            if (hadRole && !hasRole)
            {
                if (user != null)
                {
                    userList.Remove(user);
                }
            }
        }

        // Gemini API를 사용하여 이미지에서 닉네임 추출하는 메서드
        private async Task<string> GetGeminiNicknamesAsync()
        {
            // 1. 이미지 경로 확인 
            string filePath = @"tessdata\capture.png";
            string resultNicknames = "";
            if (!System.IO.File.Exists(filePath))
            {
                MessageBox.Show("파일이 없습니다! 경로를 확인해주세요: " + filePath);
                return "파일이 없소이다";
            }

            // ---------------------------------------------------------
            // 2. Gemini AI 설정 (기존 비전 API 및 JSON 설정 대체)
            // ---------------------------------------------------------
            string myApiKey = "AIzaSyDLQ1FozxGDDUWGb5wXjmOt4QTd_M-aqc8";
            var client = new Google.GenAI.Client(null, myApiKey);
            try
            {
                // 1️⃣ 이미지를 바이트 배열로 읽기
                byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);

                // 2️⃣ 데이터 조각(Part) 리스트 생성
                var parts = new List<Google.GenAI.Types.Part>
                {
                    new Google.GenAI.Types.Part { Text = "이미지에서 캐릭터 닉네임만 모두 추출해줘. " +
                                                         "반드시 각 닉네임을 쉼표(,)로 구분해서 한 줄의 문자열로만 출력해. " +
                                                         "Lv이나 숫자로 된 레벨 정보는 절대 포함하지 마. " +
                                                         "닉네임 외에 다른 설명이나 인삿말은 절대 하지 마." },
                    new Google.GenAI.Types.Part { InlineData = new Google.GenAI.Types.Blob { MimeType = "image/png", Data = imageBytes } }
                };
                // 3️⃣ [에러 해결] List<Part>를 List<Content>로 감싸서 전달
                var response = await client.Models.GenerateContentAsync(
                    "models/gemini-2.5-flash-lite",
                    new List<Google.GenAI.Types.Content>
                    {
                        new Google.GenAI.Types.Content
                        {
                            Parts = parts,
                            Role = "user" // 사용자가 보내는 메시지라는 의미
                        }
                    }
                );

                // 4️⃣ 결과 출력
                if (response.Candidates != null && response.Candidates.Count > 0)
                {
                    resultNicknames = response.Candidates[0].Content.Parts[0].Text.Trim();
                    return resultNicknames;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("오류 발생: " + ex.Message);
                return "닉네임을 추출하지 못했습니다.";
            }
            return "닉네임을 추출하지 못했습니다.";
        }

        // 디스코드 메시지에서 이미지 첨부파일을 저장하는 메서드
        public async Task SaveDiscordImageAsync(SocketMessage message)
        {
            // 1. 첨부파일이 있는지 확인
            var attachment = message.Attachments.FirstOrDefault();
            if (attachment == null) return;

            // 3. 파일명 설정 (파일 이름이 겹치지 않게 현재 시간이나 메시지 ID를 활용하면 좋습니다)
            string fileName = @"tessdata\capture.png";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 4. 이미지 URL로부터 데이터를 바이트 배열로 가져옵니다.
                    byte[] imageBytes = await client.GetByteArrayAsync(attachment.Url);

                    // 5. 하드디스크에 파일을 씁니다.
                    await File.WriteAllBytesAsync(fileName, imageBytes);

                    Console.WriteLine($"이미지 저장 완료: {fileName}");
                    // 이후 여기서 Gemini API로 다시 파일을 읽어서 보낼 수도 있습니다.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 저장 중 오류 발생: {ex.Message}");
            }
        }

        // 디스코드 메시지에 이미지 첨부파일이 있는지 확인하는 메서드
        public bool HasImageAttachment(SocketUserMessage message)
        {
            // 1. 첨부파일이 아예 없으면 false
            if (message.Attachments.Count == 0) return false;

            // 2. 첫 번째 첨부파일의 확장자를 확인 (보통 이미지는 하나씩 올리니까요)
            var attachment = message.Attachments.FirstOrDefault();

            // 이미지 확장자 리스트
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

            // 확장자가 위 리스트에 포함되어 있는지 확인 (대소문자 구분 없이)
            return imageExtensions.Any(ext => attachment.Filename.ToLower().EndsWith(ext));
        }
    }
}

public class User 
{
    public ulong Id { get; set; }
    public string Name { get; set; }    
    public bool ATO { get; set; }
    public bool BLACK { get; set; }
    public SocketGuildUser GuildUser { get; set; }
}
