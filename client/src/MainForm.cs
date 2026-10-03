using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyDownloader
{
    public class MainForm : Form
    {
        // 윈도우 드래그 이동 API
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // 마인크래프트 시그니처 색상 팔레트
        private static readonly Color ColorBedrock = Color.FromArgb(14, 16, 21);          // 심층암 딥 다크 배경
        private static readonly Color ColorDeepStone = Color.FromArgb(20, 23, 31);        // 사이드바 돌 블록
        private static readonly Color ColorStoneCard = Color.FromArgb(26, 30, 42);        // 카드 기본
        private static readonly Color ColorStoneHover = Color.FromArgb(34, 40, 56);       // 카드 호버
        private static readonly Color ColorBorderStone = Color.FromArgb(45, 52, 70);      // 블록 테두리

        // 마크 시그니처 포인트 컬러
        private static readonly Color ColorMcGreen = Color.FromArgb(34, 197, 94);         // 크리퍼 / 잔디 그린 (#22c55e)
        private static readonly Color ColorDiamondBlue = Color.FromArgb(14, 165, 233);    // 다이아몬드 원석 블루 (#0ea5e9)
        private static readonly Color ColorPortalPurple = Color.FromArgb(168, 85, 247);   // 네더 포탈 퍼플 (#a855f7)
        private static readonly Color ColorGold = Color.FromArgb(245, 158, 11);           // 금괴 골드 (#f59e0b)
        private static readonly Color ColorRedstone = Color.FromArgb(239, 68, 68);        // 레드스톤 레드 (#ef4444)
        private static readonly Color ColorTextWhite = Color.FromArgb(248, 250, 252);
        private static readonly Color ColorTextMuted = Color.FromArgb(148, 163, 184);

        // 컨테이너
        private Panel pnlTitleBar;
        private Panel pnlSidebar;
        private Panel pnlMain;
        private Panel pnlHeaderContainer; // Hero + DropBar + ListControlBar를 묶는 전용 상단 컨테이너

        // 사이드바 버튼
        private Button btnNavAll;
        private Button btnNavMods;
        private Button btnNavShaders;
        private Button btnNavPacks;
        private Button btnNavSettings;
        private Label lblEngineStatus;
        private string _activeCategory = "all";

        // 히어로 섹션
        private RoundedPanel pnlHero;
        private Label lblHeroTitle;
        private Label lblHeroSubtitle;
        private Label lblHeroBadge;
        private McBlockButton btnInstallHero;

        // 드래그 앤 드롭 바
        private Panel pnlDropBar;
        private Label lblDropText;
        private Button btnBrowseFile;

        // 모드 카드 제어 및 목록
        private Panel pnlBar;
        private DoubleBufferedFlowLayoutPanel pnlCards;
        private Button btnToggleAllOn;
        private Button btnToggleAllOff;
        private Label lblCountSummary;

        // 설정 뷰
        private RoundedPanel pnlSettingsView;
        private TextBox txtGamePath;
        private Button btnBrowseGamePath;
        private CheckBox chkFabricAuto;
        private CheckBox chkCleanOld;

        // 하단 진행 상태 & 프로그레스
        private Panel pnlFooter;
        private ModernProgressBar progressBar;
        private Label lblFooterStatus;
        private Button btnOpenFolder;
        private Button btnToggleLog;
        private TextBox txtLog;

        // 데이터
        private readonly DownloaderCore _core = new DownloaderCore();
        private ModpackConfig _config = null;
        private bool _isWorking = false;
        private string _pendingFilePath = null;
        private readonly List<ModCardControl> _cardControls = new List<ModCardControl>();

        public MainForm()
        {
            InitializeComponent();
            LoadDefaults();
        }

        public MainForm(string filePath) : this()
        {
            _pendingFilePath = filePath;
        }

        private void InitializeComponent()
        {
            this.Text = "MINECRAFT EASY MOD INSTALLER";
            this.Size = new Size(960, 640);
            this.MinimumSize = new Size(880, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = true;

            try
            {
                using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("EasyDownloader.app.ico"))
                {
                    if (stream != null)
                    {
                        this.Icon = new Icon(stream);
                    }
                    else
                    {
                        this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                }
            }
            catch { }

            this.BackColor = ColorBedrock;
            this.ForeColor = ColorTextWhite;
            this.Font = new Font("Pretendard", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            BuildTitleBar();
            BuildSidebar();
            BuildMainArea();
        }

        // =========================================================================
        // 1. 마인크래프트 옵시디언 다크 타이틀바
        // =========================================================================
        private void BuildTitleBar()
        {
            pnlTitleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = ColorDeepStone
            };
            pnlTitleBar.MouseDown += TitleBar_MouseDown;

            // 상단 마인크래프트 잔디 블록 잔디 녹색 라인 (시그니처 패턴)
            pnlTitleBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(ColorMcGreen, 2))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlTitleBar.Width, 0);
                }
            };

            // 앱 로고 아이콘
            var picLogo = new PictureBox
            {
                Size = new Size(20, 20),
                Location = new Point(14, 9),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                if (this.Icon != null)
                {
                    picLogo.Image = this.Icon.ToBitmap();
                }
            }
            catch { }
            picLogo.MouseDown += TitleBar_MouseDown;
            pnlTitleBar.Controls.Add(picLogo);

            // 타이틀 텍스트
            var lblAppTitle = new Label
            {
                Text = "MINECRAFT MOD INSTALLER",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Location = new Point(40, 10),
                AutoSize = true
            };
            lblAppTitle.MouseDown += TitleBar_MouseDown;
            pnlTitleBar.Controls.Add(lblAppTitle);

            // 마크 에디션 뱃지
            var lblVerBadge = new Label
            {
                Text = "MINECRAFT",
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = ColorMcGreen,
                BackColor = Color.FromArgb(20, 45, 30),
                Location = new Point(236, 11),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true
            };
            lblVerBadge.MouseDown += TitleBar_MouseDown;
            pnlTitleBar.Controls.Add(lblVerBadge);

            // 최소화 버튼 (─) - 닫기 버튼 왼쪽에 위치
            var btnMinimize = new Button
            {
                Text = "─",
                Size = new Size(42, 38),
                Location = new Point(pnlTitleBar.Width - 84, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTextMuted,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.MouseEnter += (s, e) => { btnMinimize.BackColor = Color.FromArgb(35, 42, 55); };
            btnMinimize.MouseLeave += (s, e) => { btnMinimize.BackColor = Color.Transparent; };
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            pnlTitleBar.Controls.Add(btnMinimize);

            // 닫기 버튼 (✕) - 맨 우측 끝에 위치
            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(42, 38),
                Location = new Point(pnlTitleBar.Width - 42, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTextMuted,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.MouseEnter += (s, e) => { btnClose.BackColor = ColorRedstone; btnClose.ForeColor = Color.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.BackColor = Color.Transparent; btnClose.ForeColor = ColorTextMuted; };
            btnClose.Click += (s, e) => Application.Exit();
            pnlTitleBar.Controls.Add(btnClose);

            this.Controls.Add(pnlTitleBar);
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000; // WS_MINIMIZEBOX (작업표시줄 클릭 시 최소화/복원 활성화)
                return cp;
            }
        }

        // =========================================================================
        // 2. 좌측 네비게이션 사이드바 (Vortex 스타일)
        // =========================================================================
        private void BuildSidebar()
        {
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 205,
                BackColor = ColorDeepStone,
                Padding = new Padding(12, 16, 12, 16)
            };

            int y = 14;

            var lblNavHeader = new Label
            {
                Text = "MINECRAFT MODS",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 105, 130),
                Location = new Point(14, y),
                AutoSize = true
            };
            pnlSidebar.Controls.Add(lblNavHeader);
            y += 24;

            btnNavAll = CreateNavButton("⛏️  전체 대시보드", y, "all");
            pnlSidebar.Controls.Add(btnNavAll);
            y += 40;

            btnNavMods = CreateNavButton("📦  모드 (Mods)", y, "mod");
            pnlSidebar.Controls.Add(btnNavMods);
            y += 40;

            btnNavShaders = CreateNavButton("🔮  쉐이더팩", y, "shader");
            pnlSidebar.Controls.Add(btnNavShaders);
            y += 40;

            btnNavPacks = CreateNavButton("🎨  리소스팩", y, "resourcepack");
            pnlSidebar.Controls.Add(btnNavPacks);
            y += 50;

            var lblSettingsHeader = new Label
            {
                Text = "GAME CONFIG",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 105, 130),
                Location = new Point(14, y),
                AutoSize = true
            };
            pnlSidebar.Controls.Add(lblSettingsHeader);
            y += 24;

            btnNavSettings = CreateNavButton("⚙️  경로 및 옵션", y, "settings");
            pnlSidebar.Controls.Add(btnNavSettings);

            // 하단 마크 로더 상태 카드
            var pnlStatusCard = new RoundedPanel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BorderRadius = 8,
                BackColor = Color.FromArgb(26, 30, 42),
                BorderColor = ColorBorderStone,
                Padding = new Padding(12)
            };

            var lblEngineTitle = new Label
            {
                Text = "LOADER STATUS",
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                Location = new Point(10, 12),
                AutoSize = true
            };
            pnlStatusCard.Controls.Add(lblEngineTitle);

            lblEngineStatus = new Label
            {
                Text = "⚪ 대기 중 (mods.txt 필요)",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                Location = new Point(10, 32),
                AutoSize = true
            };
            pnlStatusCard.Controls.Add(lblEngineStatus);

            var lblAutoTag = new Label
            {
                Text = "공식 런처 무인 프로필 등록",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(115, 130, 155),
                Location = new Point(10, 56),
                AutoSize = true
            };
            pnlStatusCard.Controls.Add(lblAutoTag);

            pnlSidebar.Controls.Add(pnlStatusCard);
            this.Controls.Add(pnlSidebar);

            UpdateNavSelection();
        }

        private Button CreateNavButton(string text, int y, string category)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(6, y),
                Size = new Size(193, 36),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                _activeCategory = category;
                UpdateNavSelection();
                FilterCards();
            };
            return btn;
        }

        private void UpdateNavSelection()
        {
            Button[] btns = { btnNavAll, btnNavMods, btnNavShaders, btnNavPacks, btnNavSettings };
            string[] cats = { "all", "mod", "shader", "resourcepack", "settings" };

            for (int i = 0; i < btns.Length; i++)
            {
                bool active = (_activeCategory == cats[i]);
                btns[i].ForeColor = active ? ColorMcGreen : ColorTextMuted;
                btns[i].BackColor = active ? Color.FromArgb(28, 36, 48) : Color.Transparent;
            }

            // 카테고리에 따라 뷰 스왑
            bool isSettings = (_activeCategory == "settings");
            if (pnlHeaderContainer != null)
            {
                pnlHeaderContainer.Visible = !isSettings;
            }
            if (pnlCards != null)
            {
                pnlCards.Visible = !isSettings;
            }
            if (pnlSettingsView != null)
            {
                pnlSettingsView.Visible = isSettings;
                if (isSettings) pnlSettingsView.BringToFront();
            }

            // 카테고리 전환 시 히어로 배너 타이틀 반응
            if (!isSettings && lblHeroTitle != null)
            {
                if (_config != null && !string.IsNullOrEmpty(_config.GameVersion))
                {
                    string baseVer = _config.GameVersion;
                    string loaderName = string.IsNullOrEmpty(_config.Loader) ? "FABRIC" : _config.Loader.ToUpperInvariant();
                    switch (_activeCategory)
                    {
                        case "mod":
                            lblHeroTitle.Text = "MINECRAFT MODS";
                            lblHeroBadge.Text = "MODS (" + _config.Entries.Count(e => e.ProjectType == "mod") + ")";
                            lblHeroBadge.BackColor = ColorDiamondBlue;
                            lblHeroBadge.ForeColor = Color.FromArgb(10, 25, 35);
                            break;
                        case "shader":
                            lblHeroTitle.Text = "MINECRAFT SHADERS";
                            lblHeroBadge.Text = "SHADERS (" + _config.Entries.Count(e => e.ProjectType == "shader") + ")";
                            lblHeroBadge.BackColor = ColorPortalPurple;
                            lblHeroBadge.ForeColor = Color.White;
                            break;
                        case "resourcepack":
                            lblHeroTitle.Text = "RESOURCE PACKS";
                            lblHeroBadge.Text = "TEXTURES (" + _config.Entries.Count(e => e.ProjectType == "resourcepack") + ")";
                            lblHeroBadge.BackColor = ColorMcGreen;
                            lblHeroBadge.ForeColor = Color.FromArgb(10, 25, 20);
                            break;
                        default:
                            lblHeroTitle.Text = "MINECRAFT " + baseVer;
                            lblHeroBadge.Text = loaderName;
                            lblHeroBadge.BackColor = ColorMcGreen;
                            lblHeroBadge.ForeColor = Color.FromArgb(10, 25, 20);
                            break;
                    }
                }
                else
                {
                    switch (_activeCategory)
                    {
                        case "mod":
                            lblHeroTitle.Text = "MINECRAFT MODS";
                            lblHeroBadge.Text = "MODS";
                            break;
                        case "shader":
                            lblHeroTitle.Text = "MINECRAFT SHADERS";
                            lblHeroBadge.Text = "SHADERS";
                            break;
                        case "resourcepack":
                            lblHeroTitle.Text = "RESOURCE PACKS";
                            lblHeroBadge.Text = "TEXTURES";
                            break;
                        default:
                            lblHeroTitle.Text = "MINECRAFT";
                            lblHeroBadge.Text = "WAITING...";
                            break;
                    }
                    lblHeroBadge.BackColor = Color.FromArgb(35, 42, 58);
                    lblHeroBadge.ForeColor = Color.FromArgb(160, 175, 200);
                }
                UpdateHeroBadgePosition();
            }
        }

        private void UpdateHeroBadgePosition()
        {
            if (lblHeroTitle != null && lblHeroBadge != null)
            {
                lblHeroBadge.Location = new Point(lblHeroTitle.Right + 12, lblHeroTitle.Top + (lblHeroTitle.Height - lblHeroBadge.Height) / 2);
            }
        }

        // =========================================================================
        // 3. 우측 메인 영역 (정확한 레이아웃 컨테이너 구조)
        // =========================================================================
        private void BuildMainArea()
        {
            pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBedrock,
                Padding = new Padding(16, 12, 16, 10)
            };
            this.Controls.Add(pnlMain);
            pnlMain.BringToFront();

            // 1. 하단 상태 바 (Dock = Bottom)
            BuildFooter();

            // 2. 상단 고정 헤더 컨테이너 (Dock = Top, 정확한 고정 좌표로 배치하여 역전 현상 원천 방지)
            BuildHeaderContainer();

            // 3. 설정 뷰 (Dock = Fill, settings 탭일 때 상단 배너를 숨기고 전체 화면 채움)
            BuildSettingsView();

            // 4. 모드 카드 스크롤 뷰 (Dock = Fill)
            pnlCards = new DoubleBufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColorBedrock,
                Padding = new Padding(0, 4, 10, 4),
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown
            };
            pnlMain.Controls.Add(pnlCards);
            pnlCards.BringToFront();
        }

        // 상단 컨테이너: Hero -> DropBar -> ListControlBar 순서로 완벽 고정
        private void BuildHeaderContainer()
        {
            pnlHeaderContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 168,
                BackColor = ColorBedrock
            };

            // 1) 히어로 배너 (Top 0)
            pnlHero = new RoundedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(pnlHeaderContainer.Width, 84),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 8,
                BorderWidth = 1,
                BorderColor = Color.FromArgb(40, 50, 70),
                BackColor = Color.FromArgb(24, 28, 38),
                Padding = new Padding(18, 14, 18, 14)
            };
            pnlHero.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, pnlHero.Width, 3),
                    ColorMcGreen,
                    ColorDiamondBlue,
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, pnlHero.Width, 3);
                }
            };

            lblHeroTitle = new Label
            {
                Text = "MINECRAFT",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Location = new Point(16, 12),
                AutoSize = true
            };
            lblHeroTitle.TextChanged += (s, e) => UpdateHeroBadgePosition();
            pnlHero.Controls.Add(lblHeroTitle);

            lblHeroBadge = new Label
            {
                Text = "WAITING...",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 175, 200),
                BackColor = Color.FromArgb(35, 42, 58),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true
            };
            lblHeroBadge.SizeChanged += (s, e) => UpdateHeroBadgePosition();
            pnlHero.Controls.Add(lblHeroBadge);
            UpdateHeroBadgePosition();

            lblHeroSubtitle = new Label
            {
                Text = "mods.txt 파일을 끌어다 놓으면 버전과 모드가 자동으로 감지됩니다",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = ColorTextMuted,
                Location = new Point(18, 46),
                AutoSize = true
            };
            pnlHero.Controls.Add(lblHeroSubtitle);

            btnInstallHero = new McBlockButton
            {
                Text = "⚡  PLAY & INSTALL",
                Size = new Size(205, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlHero.Width - 222, 18),
                Enabled = false
            };
            btnInstallHero.Click += async (s, e) => await RunInstallAsync();
            pnlHero.Controls.Add(btnInstallHero);
            pnlHeaderContainer.Controls.Add(pnlHero);

            // 2) 드래그 앤 드롭 바 (Top 90)
            pnlDropBar = new Panel
            {
                Location = new Point(0, 90),
                Size = new Size(pnlHeaderContainer.Width, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(20, 24, 34),
                AllowDrop = true
            };
            pnlDropBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(45, 65, 55), 1.5F))
                {
                    pen.DashStyle = DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, 2, 2, pnlDropBar.Width - 5, pnlDropBar.Height - 5);
                }
            };
            pnlDropBar.DragEnter += MainForm_DragEnter;
            pnlDropBar.DragDrop += MainForm_DragDrop;

            lblDropText = new Label
            {
                Text = "📂  mods.txt 파일을 이곳에 끌어다 놓으세요",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                Location = new Point(14, 11),
                AutoSize = true
            };
            lblDropText.MouseDown += (s, e) => BtnOpenFile_Click(s, e);
            pnlDropBar.Controls.Add(lblDropText);

            btnBrowseFile = new Button
            {
                Text = "파일 찾기...",
                Size = new Size(80, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlDropBar.Width - 92, 8),
                BackColor = Color.FromArgb(34, 40, 56),
                ForeColor = ColorTextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand
            };
            btnBrowseFile.FlatAppearance.BorderSize = 0;
            btnBrowseFile.Click += BtnOpenFile_Click;
            pnlDropBar.Controls.Add(btnBrowseFile);
            pnlHeaderContainer.Controls.Add(pnlDropBar);

            // 3) 리스트 제어 바 (Top 134)
            pnlBar = new Panel
            {
                Location = new Point(0, 134),
                Size = new Size(pnlHeaderContainer.Width, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = ColorBedrock
            };

            lblCountSummary = new Label
            {
                Text = "LOADED ITEMS (0)",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(115, 130, 155),
                Location = new Point(4, 7),
                AutoSize = true
            };
            pnlBar.Controls.Add(lblCountSummary);

            btnToggleAllOff = new Button
            {
                Text = "ALL OFF",
                Size = new Size(68, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlBar.Width - 74, 4),
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = ColorTextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnToggleAllOff.FlatAppearance.BorderSize = 0;
            btnToggleAllOff.Click += (s, e) => SetAllToggles(false);
            pnlBar.Controls.Add(btnToggleAllOff);

            btnToggleAllOn = new Button
            {
                Text = "ALL ON",
                Size = new Size(68, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlBar.Width - 148, 4),
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = ColorMcGreen,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnToggleAllOn.FlatAppearance.BorderSize = 0;
            btnToggleAllOn.Click += (s, e) => SetAllToggles(true);
            pnlBar.Controls.Add(btnToggleAllOn);
            pnlHeaderContainer.Controls.Add(pnlBar);

            pnlMain.Controls.Add(pnlHeaderContainer);
        }

        private void BuildFooter()
        {
            pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = ColorDeepStone,
                Padding = new Padding(16, 8, 16, 8)
            };

            progressBar = new ModernProgressBar
            {
                Dock = DockStyle.Top,
                Height = 6,
                Maximum = 100,
                Value = 0,
                StartColor = ColorDiamondBlue,
                EndColor = ColorMcGreen,
                TrackColor = Color.FromArgb(24, 28, 38)
            };
            pnlFooter.Controls.Add(progressBar);

            lblFooterStatus = new Label
            {
                Text = "준비 완료. mods.txt 파일을 불러와주세요.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = ColorTextMuted,
                Location = new Point(14, 18),
                AutoSize = true
            };
            pnlFooter.Controls.Add(lblFooterStatus);

            btnOpenFolder = new Button
            {
                Text = "📁 mods 폴더",
                Size = new Size(100, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlFooter.Width - 116, 14),
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = ColorTextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand
            };
            btnOpenFolder.FlatAppearance.BorderSize = 0;
            btnOpenFolder.Click += (s, e) =>
            {
                string p = Path.Combine(txtGamePath.Text, "mods");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                Process.Start("explorer.exe", p);
            };
            pnlFooter.Controls.Add(btnOpenFolder);

            btnToggleLog = new Button
            {
                Text = "📜 로그",
                Size = new Size(68, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlFooter.Width - 190, 14),
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = ColorTextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand
            };
            btnToggleLog.FlatAppearance.BorderSize = 0;
            btnToggleLog.Click += (s, e) =>
            {
                txtLog.Visible = !txtLog.Visible;
                txtLog.BringToFront();
            };
            pnlFooter.Controls.Add(btnToggleLog);

            pnlMain.Controls.Add(pnlFooter);

            txtLog = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(12, 14, 18),
                ForeColor = Color.FromArgb(130, 145, 170),
                Font = new Font("Consolas", 8.5F),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            pnlMain.Controls.Add(txtLog);
        }

        private void BuildSettingsView()
        {
            pnlSettingsView = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                BorderColor = ColorBorderStone,
                BackColor = Color.FromArgb(20, 24, 34),
                Padding = new Padding(28),
                Visible = false
            };

            int y = 20;

            var lblTitle = new Label
            {
                Text = "⚙️  마인크래프트 설치 경로 및 옵션 설정",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Location = new Point(24, y),
                AutoSize = true
            };
            pnlSettingsView.Controls.Add(lblTitle);
            y += 42;

            var lblDesc = new Label
            {
                Text = "모드, 쉐이더, 리소스팩이 설치될 게임 폴더와 무인 설치 옵션을 변경할 수 있습니다.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextMuted,
                Location = new Point(26, y),
                AutoSize = true
            };
            pnlSettingsView.Controls.Add(lblDesc);
            y += 38;

            var lblPath = new Label
            {
                Text = "마인크래프트 설치 경로 (.minecraft):",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColorMcGreen,
                Location = new Point(26, y),
                AutoSize = true
            };
            pnlSettingsView.Controls.Add(lblPath);
            y += 26;

            txtGamePath = new TextBox
            {
                Location = new Point(26, y),
                Size = new Size(540, 28),
                BackColor = Color.FromArgb(14, 16, 22),
                ForeColor = ColorTextWhite,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlSettingsView.Controls.Add(txtGamePath);

            btnBrowseGamePath = new Button
            {
                Text = "찾아보기...",
                Location = new Point(576, y - 1),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(34, 40, 56),
                ForeColor = ColorTextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnBrowseGamePath.FlatAppearance.BorderSize = 0;
            btnBrowseGamePath.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "마인크래프트 폴더(.minecraft)를 선택하세요.";
                    if (Directory.Exists(txtGamePath.Text)) fbd.SelectedPath = txtGamePath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK) txtGamePath.Text = fbd.SelectedPath;
                }
            };
            pnlSettingsView.Controls.Add(btnBrowseGamePath);
            y += 56;

            chkFabricAuto = new CheckBox
            {
                Text = "Fabric 로더 무인 자동 설치 (공식 마크 런처에 프로필 자동 등록)",
                Location = new Point(26, y),
                AutoSize = true,
                Checked = true,
                ForeColor = ColorTextWhite,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            pnlSettingsView.Controls.Add(chkFabricAuto);
            y += 36;

            chkCleanOld = new CheckBox
            {
                Text = "구버전 모드 자동 백업/정리 (버전 충돌 및 게임 튕김 방지)",
                Location = new Point(26, y),
                AutoSize = true,
                Checked = true,
                ForeColor = ColorTextWhite,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            pnlSettingsView.Controls.Add(chkCleanOld);

            pnlMain.Controls.Add(pnlSettingsView);
        }

        // =========================================================================
        // 4. 비즈니스 로직 & 카드 렌더링
        // =========================================================================
        private void LoadDefaults()
        {
            txtGamePath.Text = MinecraftFinder.GetDefaultMinecraftPath();

            this.Shown += delegate
            {
                // 사용자가 exe 아이콘에 mods.txt를 끌어다 놓은 경우에만 즉시 로드
                if (!string.IsNullOrEmpty(_pendingFilePath) && File.Exists(_pendingFilePath))
                {
                    LoadModpackFile(_pendingFilePath);
                }
                else
                {
                    // 메모장을 넣기 전까지는 대기 상태 유지
                    ShowEmptyStateCard();
                }
            };
        }

        private void ShowEmptyStateCard()
        {
            pnlCards.Controls.Clear();
            _cardControls.Clear();

            int cardWidth = pnlCards.ClientSize.Width - 28;
            if (cardWidth < 500) cardWidth = 680;

            var emptyCard = new RoundedPanel
            {
                Width = cardWidth,
                Height = 120,
                BorderRadius = 8,
                BorderColor = ColorBorderStone,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(20)
            };

            var lbl1 = new Label
            {
                Text = "📦  아직 로드된 모드팩이 없습니다.",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorMcGreen,
                Location = new Point(20, 24),
                AutoSize = true
            };
            emptyCard.Controls.Add(lbl1);

            var lbl2 = new Label
            {
                Text = "상단의 'mods.txt 파일을 이곳에 끌어다 놓으세요' 영역에 설정 파일을 끌어다 놓거나,\n우측의 [파일 찾기...] 버튼을 눌러 모드 목록을 로드하세요.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = ColorTextMuted,
                Location = new Point(22, 54),
                AutoSize = true
            };
            emptyCard.Controls.Add(lbl2);

            pnlCards.Controls.Add(emptyCard);
        }

        private async void LoadModpackFile(string filePath)
        {
            if (_isWorking) return;
            _isWorking = true;
            SetUIEnabled(false);

            pnlCards.Controls.Clear();
            _cardControls.Clear();
            progressBar.Value = 0;

            Log("===========================================");
            Log("파일 로드: " + Path.GetFileName(filePath));

            try
            {
                _config = ModpackParser.ParseFile(filePath);

                if (string.IsNullOrEmpty(_config.GameVersion))
                {
                    Log("[경고] VERSION 설정이 없습니다. 'VERSION=1.20.1'을 지정해주세요.");
                    _isWorking = false;
                    SetUIEnabled(true);
                    return;
                }

                string loaderUpper = string.IsNullOrEmpty(_config.Loader) ? "FABRIC" : _config.Loader.ToUpperInvariant();
                lblHeroTitle.Text = "MINECRAFT " + _config.GameVersion;
                lblHeroBadge.Text = loaderUpper;
                lblHeroBadge.BackColor = ColorMcGreen;
                lblHeroBadge.ForeColor = Color.FromArgb(10, 25, 20);
                UpdateHeroBadgePosition();

                if (lblEngineStatus != null)
                {
                    lblEngineStatus.Text = "🟢 " + loaderUpper + " 자동 설치 준비";
                    lblEngineStatus.ForeColor = ColorMcGreen;
                }

                lblDropText.Text = "📂  " + Path.GetFileName(filePath) + " (로드 완료)";
                lblFooterStatus.Text = "Modrinth API에서 최신 호환 버전을 조회 중...";

                int total = _config.Entries.Count;
                for (int i = 0; i < total; i++)
                {
                    var entry = _config.Entries[i];
                    int pct = (int)(((i + 1) * 100) / total);
                    progressBar.Value = pct;

                    if (!entry.IsDirectLink)
                    {
                        lblFooterStatus.Text = string.Format("({0}/{1}) {2} 정보 확인 중...", i + 1, total, entry.Slug);
                        await Task.Run(() => ModrinthApi.ResolveEntry(entry, _config.GameVersion, _config.Loader));
                    }
                }

                // 하위 필수 라이브러리(의존성) 자동 감지 및 추가
                lblFooterStatus.Text = "모드 실행에 필요한 필수 라이브러리 자동 분석 중...";
                await Task.Run(() => ResolveAllDependencies(_config));

                RenderModCards();

                int totalCount = _config.Entries.Count;
                int readyCount = _config.Entries.Count(e => e.Status == "ready");
                int errorCount = _config.Entries.Count(e => e.Status == "error");

                lblHeroSubtitle.Text = string.Format("총 {0}개 항목 감지됨 • {1}개 설치 준비 완료", totalCount, readyCount);
                lblFooterStatus.Text = string.Format("{0}개 모드 준비 완료. [PLAY & INSTALL]을 누르세요!", readyCount);

                btnInstallHero.Enabled = readyCount > 0;
                progressBar.Value = 100;

                Log(string.Format("준비 완료: {0}개 호환 파일 매칭 성공 (오류: {1}개)", readyCount, errorCount));
            }
            catch (Exception ex)
            {
                Log("[오류] " + ex.Message);
                lblFooterStatus.Text = "파일 로드 실패: " + ex.Message;
            }
            finally
            {
                _isWorking = false;
                SetUIEnabled(true);
            }
        }

        // 모드들의 필수 하위 의존성(required dependencies)을 재귀적으로 자동 감지하여 추가
        private void ResolveAllDependencies(ModpackConfig config)
        {
            var knownSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var knownProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in config.Entries)
            {
                if (!string.IsNullOrEmpty(e.Slug)) knownSlugs.Add(e.Slug);
                if (!string.IsNullOrEmpty(e.ProjectId)) knownProjectIds.Add(e.ProjectId);
            }

            bool foundNew;
            int depth = 0;
            do
            {
                foundNew = false;
                depth++;
                if (depth > 5) break;

                var toResolve = new List<string>();
                for (int i = 0; i < config.Entries.Count; i++)
                {
                    var e = config.Entries[i];
                    if (e.Status != "ready" || e.RequiredDependencyIds == null) continue;

                    foreach (var reqId in e.RequiredDependencyIds)
                    {
                        if (!knownProjectIds.Contains(reqId) && !toResolve.Contains(reqId))
                        {
                            toResolve.Add(reqId);
                        }
                    }
                }

                foreach (var reqId in toResolve)
                {
                    var depEntry = ModrinthApi.CreateEntryFromProjectId(reqId);
                    if (depEntry != null)
                    {
                        if (knownSlugs.Contains(depEntry.Slug))
                        {
                            knownProjectIds.Add(reqId);
                            continue;
                        }

                        ModrinthApi.ResolveEntry(depEntry, config.GameVersion, config.Loader);
                        if (depEntry.Status == "ready")
                        {
                            depEntry.IsDependency = true;
                            config.Entries.Add(depEntry);
                            knownSlugs.Add(depEntry.Slug);
                            knownProjectIds.Add(reqId);
                            foundNew = true;
                            Log(string.Format("  [필수 라이브러리 자동 감지] {0} ({1}) 추가 완료", depEntry.Title, depEntry.FileName));
                        }
                    }
                }
            } while (foundNew);
        }

        private void RenderModCards()
        {
            pnlCards.SuspendLayout();
            pnlCards.Controls.Clear();
            _cardControls.Clear();

            if (_config == null || _config.Entries.Count == 0)
            {
                ShowEmptyStateCard();
                pnlCards.ResumeLayout();
                return;
            }

            int cardWidth = pnlCards.ClientSize.Width - 28;
            if (cardWidth < 500) cardWidth = 680;

            foreach (var entry in _config.Entries)
            {
                var card = new ModCardControl(entry, cardWidth);
                _cardControls.Add(card);
                pnlCards.Controls.Add(card);
            }

            pnlCards.ResumeLayout();
            FilterCards();
        }

        private void FilterCards()
        {
            pnlCards.SuspendLayout();
            int visibleCount = 0;

            foreach (var card in _cardControls)
            {
                bool show = (_activeCategory == "all") || (card.Entry.ProjectType == _activeCategory);
                card.Visible = show;
                if (show) visibleCount++;
            }

            string catName = "ITEMS";
            if (_activeCategory == "mod") catName = "MODS";
            else if (_activeCategory == "shader") catName = "SHADERS";
            else if (_activeCategory == "resourcepack") catName = "RESOURCE PACKS";

            lblCountSummary.Text = string.Format("{0} ({1})", catName, visibleCount);

            pnlCards.ResumeLayout();
        }

        private void SetAllToggles(bool on)
        {
            foreach (var card in _cardControls)
            {
                // 현재 보이는 카테고리의 카드만 일괄 제어
                if (card.Visible && card.Entry.Status == "ready")
                {
                    card.SetToggle(on);
                }
            }
        }

        private async Task RunInstallAsync()
        {
            if (_isWorking || _config == null) return;

            string gameRoot = txtGamePath.Text.Trim();
            if (!MinecraftFinder.IsValidGameDirectory(gameRoot))
            {
                MessageBox.Show("올바른 게임 설치 폴더를 지정해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = _config.Entries.Where(e => e.IsSelected && e.Status == "ready").ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("설치할 모드가 선택되지 않았습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isWorking = true;
            SetUIEnabled(false);
            btnInstallHero.Text = "INSTALLING...";
            progressBar.Value = 0;

            Log("===========================================");
            Log(string.Format("원클릭 설치 시작: MC {0} {1} | {2}개 선택됨", _config.GameVersion, _config.Loader, selected.Count));

            int downloadedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            try
            {
                // Fabric 로더 설치
                if (chkFabricAuto.Checked && _config.Loader == "fabric")
                {
                    lblFooterStatus.Text = "Fabric 로더 설치 중...";
                    Log("Fabric 로더 확인 및 설치 중...");
                    var res = await Task.Run(() => FabricInstaller.Install(gameRoot, _config.GameVersion));
                    Log("  Fabric: " + res.Value);
                }

                var progressReporter = new Progress<DownloadProgress>(p =>
                {
                    lblFooterStatus.Text = p.Message;
                });

                var registeredByFolder = new Dictionary<string, HashSet<string>>();

                for (int i = 0; i < selected.Count; i++)
                {
                    var entry = selected[i];
                    int totalPct = (int)(((i + 1) * 100) / selected.Count);
                    progressBar.Value = totalPct;

                    if (!registeredByFolder.ContainsKey(entry.TargetFolder))
                        registeredByFolder[entry.TargetFolder] = new HashSet<string>();
                    registeredByFolder[entry.TargetFolder].Add(entry.FileName.ToLowerInvariant());

                    lblFooterStatus.Text = string.Format("({0}/{1}) {2} 다운로드 중...", i + 1, selected.Count, entry.Title);
                    Log(string.Format("  [{0}] {1} -> {2}/", entry.GetCategoryName(), entry.Title, entry.TargetFolder));

                    try
                    {
                        string prevStatus = entry.Status;
                        await _core.DownloadFileAsync(entry, gameRoot, progressReporter);

                        if (entry.Status == "done")
                        {
                            if (prevStatus == "done") skippedCount++;
                            else downloadedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Log("    -> 에러: " + ex.Message);
                    }
                }

                int cleaned = 0;
                if (chkCleanOld.Checked)
                {
                    foreach (var kvp in registeredByFolder)
                    {
                        cleaned += _core.CleanupOldFiles(gameRoot, kvp.Key, kvp.Value);
                    }
                    if (cleaned > 0) Log(string.Format("구버전 {0}개 정리 완료", cleaned));
                }

                progressBar.Value = 100;
                lblFooterStatus.Text = "모든 모드 설치 완료! 마인크래프트를 시작하세요.";
                Log("===========================================");
                Log(string.Format("완료! 신규: {0}, 최신스킵: {1}, 오류: {2}, 구버전정리: {3}", downloadedCount, skippedCount, errorCount, cleaned));

                MessageBox.Show(
                    string.Format("모든 모드 설치가 성공적으로 완료되었습니다!\n\n- 설치/업데이트: {0}개\n- 이미 최신: {1}개\n\n마인크래프트 런처에서 'Fabric {2}' 프로필을 선택해 플레이하세요!",
                    downloadedCount, skippedCount, _config.GameVersion),
                    "설치 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblFooterStatus.Text = "설치 중 에러 발생";
                Log("[오류] " + ex.Message);
                MessageBox.Show("설치 도중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isWorking = false;
                SetUIEnabled(true);
                btnInstallHero.Text = "⚡  PLAY & INSTALL";
            }
        }

        private void SetUIEnabled(bool enabled)
        {
            btnBrowseFile.Enabled = enabled;
            btnInstallHero.Enabled = enabled && _config != null && _config.Entries.Any(e => e.Status == "ready");
            btnToggleAllOn.Enabled = enabled;
            btnToggleAllOff.Enabled = enabled;
        }

        private void BtnOpenFile_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "모드팩 설정 파일 선택";
                ofd.Filter = "모드팩 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadModpackFile(ofd.FileName);
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                pnlDropBar.BackColor = Color.FromArgb(28, 48, 38);
            }
            else e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            pnlDropBar.BackColor = Color.FromArgb(20, 24, 34);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && files[0].EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                LoadModpackFile(files[0]);
            }
        }

        private void Log(string msg)
        {
            string line = string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg);
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => {
                    txtLog.AppendText(line);
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                }));
            }
            else
            {
                txtLog.AppendText(line);
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
            }
        }
    }

    // =========================================================================
    // 5. 개별 모드 카드 컨트롤 (마인크래프트 테마 + WeMod 슬라이더)
    // =========================================================================
    public class ModCardControl : RoundedPanel
    {
        public ModEntry Entry { get; private set; }
        private ToggleSwitch _toggle;

        public ModCardControl(ModEntry entry, int width)
        {
            this.Entry = entry;
            this.Width = width;
            this.Height = 58;
            this.BorderRadius = 8;
            this.BorderWidth = 1;
            this.BorderColor = Color.FromArgb(38, 45, 60);
            this.BackColor = Color.FromArgb(24, 28, 38);
            this.Margin = new Padding(0, 0, 0, 8);
            this.Cursor = Cursors.Default;

            BuildCard();
        }

        private void BuildCard()
        {
            Color badgeBg, badgeFg;
            string badgeText = Entry.GetCategoryName().ToUpperInvariant();

            if (Entry.IsDependency)
            {
                badgeBg = Color.FromArgb(60, 45, 15);
                badgeFg = Color.FromArgb(250, 204, 21);
                badgeText = "필수 라이브러리";
            }
            else if (Entry.ProjectType == "shader")
            {
                badgeBg = Color.FromArgb(50, 30, 80);
                badgeFg = Color.FromArgb(192, 132, 252);
            }
            else if (Entry.ProjectType == "resourcepack")
            {
                badgeBg = Color.FromArgb(20, 50, 35);
                badgeFg = Color.FromArgb(52, 211, 153);
            }
            else
            {
                badgeBg = Color.FromArgb(15, 45, 65);
                badgeFg = Color.FromArgb(56, 189, 248);
            }

            var lblBadge = new Label
            {
                Text = badgeText,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = badgeFg,
                BackColor = badgeBg,
                Location = new Point(14, 18),
                Padding = new Padding(5, 2, 5, 2),
                AutoSize = true
            };
            this.Controls.Add(lblBadge);

            string titleText = string.IsNullOrEmpty(Entry.Title) ? Entry.Slug : Entry.Title;
            var lblTitle = new Label
            {
                Text = titleText,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(90, 11),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            string subText = Entry.FileName;
            string size = Entry.GetSizeText();
            if (!string.IsNullOrEmpty(size)) subText += "  •  " + size;
            if (Entry.Status == "error") subText = "⚠️ " + Entry.ErrorMessage;

            var lblSub = new Label
            {
                Text = subText,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = (Entry.Status == "error") ? Color.FromArgb(248, 113, 113) : Color.FromArgb(140, 155, 180),
                Location = new Point(92, 32),
                AutoSize = true
            };
            this.Controls.Add(lblSub);

            _toggle = new ToggleSwitch
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.Width - 70, 16),
                Checked = Entry.IsSelected && (Entry.Status != "error"),
                OnColor = Color.FromArgb(34, 197, 94)
            };
            if (Entry.Status == "error")
            {
                _toggle.Enabled = false;
                _toggle.Checked = false;
            }
            _toggle.CheckedChanged += (s, e) =>
            {
                Entry.IsSelected = _toggle.Checked;
            };
            this.Controls.Add(_toggle);

            this.MouseEnter += (s, e) => this.BackColor = Color.FromArgb(32, 38, 52);
            this.MouseLeave += (s, e) => this.BackColor = Color.FromArgb(24, 28, 38);
        }

        public void SetToggle(bool on)
        {
            if (_toggle != null && _toggle.Enabled)
            {
                _toggle.Checked = on;
            }
        }
    }
}
