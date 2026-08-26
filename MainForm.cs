using System.Drawing;
using System.Drawing.Drawing2D;
using GitHubDeploymentManager.Models;
using GitHubDeploymentManager.Services;
using ToastNotification;

namespace GitHubDeploymentManager;

public sealed class MainForm : Form
{
    private static readonly Color AppBackgroundColor = Color.FromArgb(246, 248, 252);
    private static readonly Color SurfaceColor = Color.FromArgb(255, 255, 255);
    private static readonly Color SurfaceMutedColor = Color.FromArgb(242, 246, 255);
    private static readonly Color BorderColor = Color.FromArgb(223, 229, 240);
    private static readonly Color AccentColor = Color.FromArgb(79, 70, 229);
    private static readonly Color AccentHoverColor = Color.FromArgb(67, 56, 202);
    private static readonly Color AccentSoftColor = Color.FromArgb(224, 231, 255);
    private static readonly Color TextPrimaryColor = Color.FromArgb(15, 23, 42);
    private static readonly Color TextSecondaryColor = Color.FromArgb(71, 85, 105);
    private static readonly Color SuccessColor = Color.FromArgb(34, 197, 94);
    private static readonly Color WarningColor = Color.FromArgb(245, 158, 11);
    private static readonly Color ErrorColor = Color.FromArgb(239, 68, 68);

    private readonly MainFormController _controller;
    private readonly AppConfiguration _config;
    private readonly IAppLogger _logger;
    private IGitHubService _github;

    private readonly TextBox _patText = new();
    private readonly Button _applyPatButton = new();
    private readonly Label _patStatusLabel = new();
    private readonly Panel _patOptionsPanel = new Panel();
    private readonly Button _patToggleButton = new Button();
    private readonly System.Windows.Forms.Timer _patToggleTimer = new System.Windows.Forms.Timer();
    private bool _patPanelExpanded = false;
    private readonly TextBox _ownerText = new();
    private readonly Button _fetchButton = new();
    private readonly FlowLayoutPanel _releasePanel = new();
    private readonly Label _releaseStatus = new();
    private readonly TextBox _pathText = new();
    private readonly Button _browseButton = new();
    private readonly Button _testPathButton = new();
    private readonly Label _pathStatus = new();
    private readonly Button _deployButton = new();
    private readonly Button _cancelDeployButton = new();
    private readonly Button _clearButton = new();
    private bool _cancelledByUser;
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _assetLabel = new();
    private readonly Label _packageTypeLabel = new();
    private readonly Label _packageNotesLabel = new();
    private int _repoColumns = 1;
    private Panel? _repoGridHost;
    private TableLayoutPanel? _repoGrid;
    private Panel? _scrollHost;
    private Point _savedScrollPosition = Point.Empty;
    private Control? _step1Card;
    private Control? _savedFocusedControl;
    private Control? _step2Card;
    private Control? _step3Card;
    private Control? _deploySection;
    private Control? _statusSection;

    private List<GitHubRepository> _repositories = [];
    private List<GitHubRelease> _releases = [];
    private HashSet<string> _repositoriesWithoutReleases = new();
    private GitHubRepository? _selectedRepository;
    private GitHubRelease? _selectedRelease;
    private GitHubReleasePackage? _selectedPackage;
    private CancellationTokenSource? _operationCts;

    public MainForm(MainFormController controller, IAppLogger logger)
    {
        _controller = controller;
        _config = controller.Configuration;
        _logger = logger;
        _github = _controller.CreateGitHubService(_config.GitHubToken);

        Text = "GitHub Deployment Manager";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1040, 820);
        Size = new Size(1200, 900);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = AppBackgroundColor;
        Padding = new Padding(1);
        DoubleBuffered = true;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        BuildUi();
        ResetReleaseState();
        ResetPathState();

        _patText.Text = _config.GitHubToken;
        ApplyGitHubToken(initialLoad: true);
        _ownerText.Text = _config.GitHubOwner;
        _pathText.Text = _config.DefaultDeploymentPath;
        UpdateDeployButton();
        
        Shown += async (_, _) =>
        {
            BeginInvoke(new Action(ScrollToTop));
            await TestDeploymentPathAsync();
        };
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            base.OnPaintBackground(e);
            return;
        }

        using var backgroundBrush = new LinearGradientBrush(
            rect,
            Color.FromArgb(249, 250, 255),
            Color.FromArgb(236, 242, 255),
            LinearGradientMode.Vertical);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillRectangle(backgroundBrush, rect);

        using var glowBrush = new SolidBrush(Color.FromArgb(42, Color.White));
        e.Graphics.FillEllipse(glowBrush, -Width / 5, -Height / 4, Width, Height);
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI Semibold", 9.5F),
        ForeColor = TextSecondaryColor,
        Margin = new Padding(0, 0, 0, 8)
    };

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = SurfaceMutedColor;
        textBox.ForeColor = TextPrimaryColor;
        textBox.Margin = new Padding(0, 0, 12, 10);
    }

    private void ApplyGitHubToken(bool initialLoad = false)
    {
        var token = _patText.Text.Trim();
        _github = _controller.CreateGitHubService(token);
        _github.ClearCache();

        if (string.IsNullOrWhiteSpace(token))
        {
            _patStatusLabel.Text = "PAT cleared. Public GitHub API limit: 60 req/hr. Click Fetch to reload.";
            _patStatusLabel.ForeColor = WarningColor;

            if (!initialLoad)
            {
                ResetRepositories();
                SetStatus("PAT cleared. GitHub requests will use the public 60 req/hr limit.", true);
            }

            return;
        }

        _patStatusLabel.Text = initialLoad
            ? "PAT loaded. Authenticated GitHub API limit: 5,000 req/hr."
            : "PAT applied. Authenticated GitHub API limit: 5,000 req/hr. Click Fetch to reload.";
        _patStatusLabel.ForeColor = SuccessColor;

        if (!initialLoad)
        {
            ResetRepositories();
            SetStatus("PAT applied successfully. Authenticated GitHub requests now use the 5,000 req/hr limit.", true);
        }
        }

    private void TogglePatPanel()
    {
        if (_patOptionsPanel is null) return;

// If Step 1 is collapsed, expand it first so the PAT controls are visible within the flow.
        try
        {
            if (_step1Card is not null)
            {
                var glass = _step1Card.Controls.Count > 0 ? _step1Card.Controls[0] as Panel : null;
                var layout = glass?.Controls.Count > 0 ? glass.Controls[0] as TableLayoutPanel : null;
                var contentHost = layout?.Controls.Count > 1 ? layout.Controls[1] as Panel : null;
                var headerRow = layout?.Controls.Count > 0 ? layout.Controls[0] as TableLayoutPanel : null;
                var toggleBtn = headerRow?.Controls.Count > 1 ? headerRow.Controls[1] as Button : null;

                if (contentHost is not null && !contentHost.Visible)
                {
                    contentHost.Visible = true;
                    if (toggleBtn is not null)
                        toggleBtn.Text = "▲";
                }
            }
        }
        catch
        {
            // Ignore any reflection-like issues and continue toggling the PAT panel.
        }

if (!_patPanelExpanded)
        {
            // Prepare and start expand animation.
            _patOptionsPanel.Visible = true;
            _patOptionsPanel.Height = 0;
            var content = _patOptionsPanel.Controls.Count > 0 ? _patOptionsPanel.Controls[0] : null;
            var target = content is Control c ? c.GetPreferredSize(Size.Empty).Height : 120;
            _patOptionsPanel.Tag = target;
        }
        else
        {
            // Collapse to height 0.
            _patOptionsPanel.Tag = 0;
        }

        _patToggleTimer.Interval = 12;
        _patToggleTimer.Tick -= PatToggleTimer_Tick;
        _patToggleTimer.Tick += PatToggleTimer_Tick;
        _patToggleTimer.Start();
    }

    private void PatToggleTimer_Tick(object? sender, EventArgs e)
    {
        if (_patOptionsPanel is null) return;
        var target = _patOptionsPanel.Tag is int t ? t : 0;
        var step = Math.Max(4, target / 12);

        if (!_patPanelExpanded)
        {
            var next = Math.Min(target, _patOptionsPanel.Height + step);
            _patOptionsPanel.Height = next;
            if (next >= target)
            {
                _patToggleTimer.Stop();
                _patPanelExpanded = true;
            }
        }
        else
        {
            var next = Math.Max(0, _patOptionsPanel.Height - step);
            _patOptionsPanel.Height = next;
            if (next <= 0)
            {
                _patToggleTimer.Stop();
                _patOptionsPanel.Visible = false;
                _patPanelExpanded = false;
            }
        }
    }

    private void ResetRepositories()
    {
        _repositories = [];
        _repositoriesWithoutReleases.Clear();
        _selectedRepository = null;
        ResetReleaseState();
        _github.ClearCache();
        UpdateRepositoryGridLayout();
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? AccentHoverColor : BorderColor;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHoverColor : Color.FromArgb(243, 247, 255);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(67, 56, 202) : Color.FromArgb(232, 238, 255);
        button.BackColor = primary ? AccentColor : SurfaceColor;
        button.ForeColor = primary ? Color.White : TextPrimaryColor;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0, 0, 0, 10);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleOptionPanel(FlowLayoutPanel panel, FlowDirection direction)
    {
        panel.AutoScroll = true;
        panel.WrapContents = direction == FlowDirection.LeftToRight;
        panel.FlowDirection = direction;
        panel.Padding = new Padding(4);
        panel.BackColor = SurfaceMutedColor;
        panel.Margin = new Padding(0);
    }

    private static void StyleInfoLabel(Label label)
    {
        label.AutoSize = true;
        label.Font = new Font("Segoe UI", 9.5F);
        label.ForeColor = TextSecondaryColor;
        label.Margin = new Padding(0, 0, 0, 8);
    }

    private static void StyleBadgeLabel(Label label, Color backColor, Color foreColor)
    {
        label.AutoSize = true;
        label.Font = new Font("Segoe UI Semibold", 9F);
        label.BackColor = backColor;
        label.ForeColor = foreColor;
        label.Padding = new Padding(10, 5, 10, 5);
        label.Margin = new Padding(0, 0, 0, 8);
    }

    private void UpdateDynamicSectionHeights()
    {
        SetDynamicPanelHeight(_releasePanel, 108, 320);
    }

    private static void SetDynamicPanelHeight(FlowLayoutPanel panel, int minimumHeight, int maximumHeight)
    {
        panel.Height = CalculateDynamicPanelHeight(panel, minimumHeight, maximumHeight);
    }

    private static int CalculateDynamicPanelHeight(FlowLayoutPanel panel, int minimumHeight, int maximumHeight)
    {
        if (panel.Controls.Count == 0)
            return minimumHeight;

        if (panel.FlowDirection == FlowDirection.TopDown)
        {
            var totalHeight = panel.Padding.Vertical;
            foreach (Control control in panel.Controls)
            {
                totalHeight += control.Height + control.Margin.Vertical;
            }

            return Math.Clamp(totalHeight + 4, minimumHeight, maximumHeight);
        }

        var availableWidth = Math.Max(panel.ClientSize.Width, panel.Width);
        var usableWidth = Math.Max(availableWidth - panel.Padding.Horizontal - 4, 220);
        var currentLineWidth = 0;
        var currentLineHeight = 0;
        var totalWrappedHeight = panel.Padding.Vertical;

        foreach (Control control in panel.Controls)
        {
            var preferredSize = control.GetPreferredSize(Size.Empty);
            var controlWidth = preferredSize.Width + control.Margin.Horizontal;
            var controlHeight = preferredSize.Height + control.Margin.Vertical;

            if (currentLineWidth > 0 && currentLineWidth + controlWidth > usableWidth)
            {
                totalWrappedHeight += currentLineHeight;
                currentLineWidth = 0;
                currentLineHeight = 0;
            }

            currentLineWidth += controlWidth;
            currentLineHeight = Math.Max(currentLineHeight, controlHeight);
        }

        totalWrappedHeight += currentLineHeight + 4;
        return Math.Clamp(totalWrappedHeight, minimumHeight, maximumHeight);
    }

    private static void StyleOptionRadio(RadioButton radio)
    {
        radio.Appearance = Appearance.Normal;
        radio.AutoSize = true;
        radio.FlatStyle = FlatStyle.Standard;
        radio.UseVisualStyleBackColor = false;
        radio.BackColor = SurfaceMutedColor;
        radio.ForeColor = TextPrimaryColor;
        radio.Font = new Font("Segoe UI", 9F);
        radio.Padding = new Padding(6, 4, 6, 4);
        radio.TextAlign = ContentAlignment.MiddleLeft;
        radio.Cursor = Cursors.Hand;
        ApplyOptionState(radio);
        radio.CheckedChanged += (_, _) => ApplyOptionState(radio);
    }

    private static void ApplyOptionState(RadioButton radio)
    {
        radio.BackColor = radio.Checked ? AccentSoftColor : SurfaceMutedColor;
        radio.ForeColor = radio.Checked ? AccentColor : TextPrimaryColor;
        radio.Font = new Font("Segoe UI", 9F, radio.Checked ? FontStyle.Bold : FontStyle.Regular);
    }

    private static int CalculateRepositoryColumns(int itemCount, int availableWidth)
    {
        if (itemCount <= 0)
            return 1;

        // Show as a simple single-column list when there are fewer than 5 repos.
        if (itemCount < 5)
            return 1;

        var targetColumns = Math.Max(1, availableWidth / 260);
        return Math.Clamp(targetColumns, 1, Math.Min(itemCount, 6));
    }

    private static Panel CreateSectionCard(string stepNumber, string title, string description, Control content)
    {
        content.Dock = DockStyle.Top;
        content.Margin = new Padding(0);

        var contentHost = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 0, 20, 18),
            BackColor = Color.FromArgb(250, 252, 255)
        };
        contentHost.Controls.Add(content);

        var stepLabel = new Label
        {
            AutoSize = true,
            Text = stepNumber,
            ForeColor = AccentColor,
            Font = new Font("Segoe UI Semibold", 9F),
            BackColor = Color.FromArgb(232, 236, 255),
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 0, 10)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = TextPrimaryColor,
            Margin = new Padding(0, 0, 0, 4)
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            Text = description,
            Font = new Font("Segoe UI", 9F),
            ForeColor = TextSecondaryColor,
            Margin = new Padding(0, 0, 0, 12)
        };

        var headerInfo = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        headerInfo.Controls.Add(stepLabel);
        headerInfo.Controls.Add(titleLabel);
        headerInfo.Controls.Add(descriptionLabel);

        var toggleBtn = new Button
        {
            Text = "▲",
            AutoSize = false,
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8F),
            ForeColor = TextSecondaryColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(4, 10, 10, 0),
            TabStop = false
        };
        toggleBtn.FlatAppearance.BorderSize = 0;
        toggleBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, AccentColor);
        toggleBtn.Click += (_, _) =>
        {
            contentHost.Visible = !contentHost.Visible;
            toggleBtn.Text = contentHost.Visible ? "▲" : "▼";
        };

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(20, 18, 10, 0)
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerRow.Controls.Add(headerInfo, 0, 0);
        headerRow.Controls.Add(toggleBtn, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(headerRow, 0, 0);
        layout.Controls.Add(contentHost, 0, 1);

        var shine = new Panel
        {
            Dock = DockStyle.Top,
            Height = 2,
            BackColor = Color.FromArgb(140, Color.White)
        };

        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BorderColor,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(1)
        };

        var glass = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(249, 251, 255),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        glass.Controls.Add(layout);
        glass.Controls.Add(shine);

        card.Controls.Add(glass);
        return card;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppBackgroundColor,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppBackgroundColor
        };
        root.Controls.Add(_scrollHost, 0, 0);

        // Track the user's scroll position so it can be restored after transient focus changes
        _scrollHost.Scroll += (_, _) =>
        {
            if (_scrollHost is null) return;
            // AutoScrollPosition returns negative coordinates; invert to get positive offsets
            var pos = _scrollHost.AutoScrollPosition;
            _savedScrollPosition = new Point(-pos.X, -pos.Y);
        };

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(28, 24, 28, 20),
            BackColor = AppBackgroundColor,
            Margin = new Padding(0)
        };

        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));

        _scrollHost.Controls.Add(main);
        main.Width = _scrollHost.ClientSize.Width;
        _scrollHost.Resize += (_, _) =>
        {
            main.Width = _scrollHost.ClientSize.Width - (_scrollHost.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0);
            UpdateDynamicSectionHeights();

            // Only rebuild the repo grid if the number of columns would change.
            // Rebuilding unconditionally destroys the checked radio button whenever
            // the scroll panel resizes due to a step becoming visible/hidden.
            if (_repoGridHost is not null && _repositories.Count > 0)
            {
                var newColumns = CalculateRepositoryColumns(_repositories.Count, _repoGridHost.Width);
                if (newColumns != _repoColumns)
                    UpdateRepositoryGridLayout();
            }
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = AppBackgroundColor,
            Margin = new Padding(0, 0, 0, 16)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Text = "GitHub Deployment Manager",
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = TextPrimaryColor,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 2)
        };

        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Professional release deployment for GitHub repositories and validated target environments.",
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondaryColor,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0)
        };

        _patToggleButton.Text = "PAT";
        _patToggleButton.AutoSize = true;
        StyleButton(_patToggleButton);
        _patToggleButton.Margin = new Padding(8, 0, 8, 0);
        _patToggleButton.Click += (_, _) => TogglePatPanel();

        _clearButton.Text = "Clear";
        _clearButton.AutoSize = true;
        StyleButton(_clearButton);
        _clearButton.Margin = new Padding(0, 0, 0, 0);
        _clearButton.Click += async (_, _) => await ResetAppAsync();

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);
        header.Controls.Add(_patToggleButton, 1, 0);
        header.Controls.Add(_clearButton, 2, 0);
        header.SetRowSpan(_patToggleButton, 2);
        header.SetRowSpan(_clearButton, 2);
        main.Controls.Add(header, 0, 0);

        _step1Card = BuildGitHubSection();
        _step2Card = BuildReleaseSection();
        _step3Card = BuildTargetSection();
        _deploySection = BuildDeploySection();
        _statusSection = BuildStatusSection();

        if (_step1Card is not null) _step1Card.Visible = true;
        if (_step2Card is not null) _step2Card.Visible = false;
        if (_step3Card is not null) _step3Card.Visible = false;
        if (_deploySection is not null) _deploySection.Visible = false;
        if (_statusSection is not null) _statusSection.Visible = true;

        if (_step1Card is not null) main.Controls.Add(_step1Card, 0, 1);
        if (_step2Card is not null) main.Controls.Add(_step2Card, 0, 2);
        if (_step3Card is not null) main.Controls.Add(_step3Card, 0, 3);
        if (_deploySection is not null) main.Controls.Add(_deploySection, 0, 4);
        if (_statusSection is not null) main.Controls.Add(_statusSection, 0, 5);

        _progress.Dock = DockStyle.Fill;
        _progress.Height = 12;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.MarqueeAnimationSpeed = 0;
        _progress.Visible = false;
        _progress.Maximum = 100;
        _progress.Value = 0;
        main.Controls.Add(_progress, 0, 6);

        UpdateDynamicSectionHeights();
        UpdateRepositoryGridLayout();
        UpdateWorkflowVisibility();
    }

    private Control BuildGitHubSection()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 6,
            BackColor = SurfaceColor,
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // PAT options are hidden by default and slide out when the PAT button is clicked.
        var patLabel = CreateFieldLabel("Personal access token (optional)");

        // Host panel for animation. Inner content is autosized; this host animates height.
        _patOptionsPanel.AutoSize = false;
        _patOptionsPanel.Height = 0;
        _patOptionsPanel.Visible = false;
        _patOptionsPanel.Dock = DockStyle.Top;

        var patInner = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 3,
            BackColor = SurfaceColor,
            Margin = new Padding(0)
        };
        patInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        patInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        patInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        patInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        patInner.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        patInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        patInner.Controls.Add(patLabel, 0, 0);
        patInner.SetColumnSpan(patLabel, 3);

        _patText.Dock = DockStyle.Fill;
        _patText.UseSystemPasswordChar = true;
        StyleTextBox(_patText);
        patInner.Controls.Add(_patText, 0, 1);
        patInner.SetColumnSpan(_patText, 2);

        _applyPatButton.Text = "Apply PAT";
        _applyPatButton.Dock = DockStyle.Fill;
        StyleButton(_applyPatButton, primary: true);
        _applyPatButton.Click += (_, _) => ApplyGitHubToken();
        patInner.Controls.Add(_applyPatButton, 2, 1);

        _patStatusLabel.Dock = DockStyle.Fill;
        _patStatusLabel.AutoSize = true;
        _patStatusLabel.Font = new Font("Segoe UI", 9.5F);
        _patStatusLabel.Margin = new Padding(0, 0, 0, 12);
        patInner.Controls.Add(_patStatusLabel, 0, 2);
        patInner.SetColumnSpan(_patStatusLabel, 3);

        _patOptionsPanel.Controls.Add(patInner);
        table.Controls.Add(_patOptionsPanel, 0, 0);
        table.SetColumnSpan(_patOptionsPanel, 3);

        table.Controls.Add(CreateFieldLabel("https://github.com/"), 0, 3);

        _ownerText.Dock = DockStyle.Fill;
        StyleTextBox(_ownerText);
        table.Controls.Add(_ownerText, 1, 3);

        _fetchButton.Text = "Fetch repositories";
        _fetchButton.Dock = DockStyle.Fill;
        StyleButton(_fetchButton, primary: true);
        _fetchButton.Click += async (_, _) => await FetchRepositoriesAsync();
        table.Controls.Add(_fetchButton, 2, 3);

        _repoGridHost = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            BackColor = SurfaceMutedColor,
            Margin = new Padding(0),
            Padding = new Padding(4)
        };
        table.Controls.Add(_repoGridHost, 0, 4);
        table.SetColumnSpan(_repoGridHost, 3);

        _repoGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = SurfaceMutedColor,
            GrowStyle = TableLayoutPanelGrowStyle.AddColumns
        };
        _repoGridHost.Controls.Add(_repoGrid);

        return CreateSectionCard(
            "Step 1",
            "Select the GitHub source",
            "Use a GitHub PAT for higher API limits, then load repositories from the selected account.",
            table);
    }

    private Control BuildReleaseSection()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SurfaceColor,
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _releasePanel.Dock = DockStyle.Top;
        _releasePanel.MinimumSize = new Size(0, 88);
        StyleOptionPanel(_releasePanel, FlowDirection.TopDown);
        table.Controls.Add(_releasePanel, 0, 0);

        // Right-side info panel: status + package details stacked tightly
        var infoPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4, 8, 4, 8),
            BackColor = SurfaceColor
        };

        _releaseStatus.AutoSize = true;
        _releaseStatus.MaximumSize = new Size(280, 0);
        _releaseStatus.Font = new Font("Segoe UI Semibold", 10.5F);
        _releaseStatus.ForeColor = TextSecondaryColor;
        _releaseStatus.Margin = new Padding(0, 0, 0, 6);
        infoPanel.Controls.Add(_releaseStatus);

        StyleBadgeLabel(_packageTypeLabel, AccentSoftColor, AccentColor);
        _packageTypeLabel.Margin = new Padding(0, 0, 0, 4);
        infoPanel.Controls.Add(_packageTypeLabel);

        StyleInfoLabel(_packageNotesLabel);
        _packageNotesLabel.Margin = new Padding(0, 0, 0, 4);
        infoPanel.Controls.Add(_packageNotesLabel);

        _assetLabel.AutoSize = true;
        _assetLabel.MaximumSize = new Size(280, 0);
        _assetLabel.Font = new Font("Segoe UI", 9.5F);
        _assetLabel.ForeColor = TextSecondaryColor;
        infoPanel.Controls.Add(_assetLabel);

        table.Controls.Add(infoPanel, 1, 0);

        return CreateSectionCard(
            "Step 2",
            "Choose the release package",
            "Validate the published release package before deployment begins.",
            table);
    }

    private Control BuildTargetSection()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = SurfaceColor,
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        table.Controls.Add(CreateFieldLabel("Target path"), 0, 0);

        _pathText.Dock = DockStyle.Fill;
        StyleTextBox(_pathText);
        _pathText.TextChanged += (_, _) => ResetPathState();
        table.Controls.Add(_pathText, 1, 0);

        _browseButton.Text = "Browse";
        _browseButton.Dock = DockStyle.Fill;
        StyleButton(_browseButton);
        _browseButton.Click += (_, _) => BrowseTarget();
        table.Controls.Add(_browseButton, 2, 0);

        _testPathButton.Text = "Test connection";
        _testPathButton.Dock = DockStyle.Fill;
        StyleButton(_testPathButton);
        _testPathButton.Click += async (_, _) => await TestDeploymentPathAsync();
        table.Controls.Add(_testPathButton, 3, 0);

        _pathStatus.AutoSize = true;
        _pathStatus.Dock = DockStyle.Top;
        _pathStatus.Font = new Font("Segoe UI Semibold", 10F);
        _pathStatus.ForeColor = TextSecondaryColor;
        _pathStatus.Margin = new Padding(0, 6, 0, 6);
        table.Controls.Add(_pathStatus, 1, 1);
        table.SetColumnSpan(_pathStatus, 3);

        return CreateSectionCard(
            "Step 3",
            "Confirm the deployment destination",
            "Use a local folder or network share and verify connectivity before deployment.",
            table);
    }

    private Control BuildDeploySection()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppBackgroundColor };

        // Deploy button
        _deployButton.Text = "Deploy release";
        _deployButton.Width = 220;
        _deployButton.Height = 48;
        _deployButton.Font = new Font("Segoe UI Semibold", 11F);
        StyleButton(_deployButton, primary: true);
        _deployButton.Click += async (_, _) => await DeployAsync();

        // Cancel button — visible only while deployment is running
        _cancelDeployButton.Text = "✕  Cancel deployment";
        _cancelDeployButton.Width = 200;
        _cancelDeployButton.Height = 48;
        _cancelDeployButton.Font = new Font("Segoe UI Semibold", 11F);
        _cancelDeployButton.FlatStyle = FlatStyle.Flat;
        _cancelDeployButton.FlatAppearance.BorderSize = 1;
        _cancelDeployButton.FlatAppearance.BorderColor = Color.FromArgb(185, 28, 28);
        _cancelDeployButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 28, 28);
        _cancelDeployButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(153, 27, 27);
        _cancelDeployButton.BackColor = ErrorColor;
        _cancelDeployButton.ForeColor = Color.White;
        _cancelDeployButton.Margin = new Padding(12, 0, 0, 10);
        _cancelDeployButton.Cursor = Cursors.Hand;
        _cancelDeployButton.Visible = false;
        _cancelDeployButton.Click += (_, _) =>
        {
            _cancelledByUser = true;
            _operationCts?.Cancel();
        };

        // Row that centres both buttons together
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppBackgroundColor,
            Margin = new Padding(0)
        };
        row.Controls.Add(_deployButton);
        row.Controls.Add(_cancelDeployButton);

        panel.Controls.Add(row);

        // Keep the row centred whenever the panel resizes
        void CentreRow(object? s, EventArgs e) =>
            row.Location = new Point((panel.ClientSize.Width - row.Width) / 2, 20);

        panel.Resize += CentreRow;
        row.ClientSizeChanged += CentreRow;

        return panel;
    }

    private void UpdateRepositoryGridLayout()
    {
        if (_repoGridHost is null || _repoGrid is null)
            return;

        _repoGrid.SuspendLayout();
        try
        {
            _repoGrid.Controls.Clear();
            _repoGrid.ColumnStyles.Clear();
            _repoGrid.RowStyles.Clear();
            _repoGrid.ColumnCount = 0;
            _repoGrid.RowCount = 0;

            if (_repositories.Count == 0)
            {
                var emptyLabel = new Label
                {
                    AutoSize = true,
                    Text = "No repositories loaded yet.",
                    ForeColor = TextSecondaryColor,
                    Margin = new Padding(4)
                };
                _repoGrid.ColumnCount = 1;
                _repoGrid.RowCount = 1;
                _repoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _repoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _repoGrid.Controls.Add(emptyLabel, 0, 0);
                return;
            }

            var availableWidth = Math.Max(_repoGridHost.ClientSize.Width, _repoGridHost.Width);
            _repoColumns = CalculateRepositoryColumns(_repositories.Count, availableWidth);
            var rowCount = (int)Math.Ceiling(_repositories.Count / (double)_repoColumns);

            _repoGrid.ColumnCount = _repoColumns;
            _repoGrid.RowCount = rowCount;

            for (var column = 0; column < _repoColumns; column++)
                _repoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _repoColumns));

            for (var row = 0; row < rowCount; row++)
                _repoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            for (var index = 0; index < _repositories.Count; index++)
            {
                var repo = _repositories[index];
                var column = index / rowCount;
                var row = index % rowCount;

                var radio = new RadioButton
                {
                    Text = repo.Name,
                    Tag = repo,
                    AutoSize = true,
                    Margin = new Padding(4, 4, 16, 4),
                    AutoEllipsis = true,
                    UseCompatibleTextRendering = true
                };
                StyleOptionRadio(radio);

                EventHandler onCheckedChanged = async (_, _) =>
                {
                    if (!radio.Checked) return;
                    _selectedRepository = (GitHubRepository)radio.Tag;
                    await LoadReleasesAsync(_selectedRepository);
                };
                radio.CheckedChanged += onCheckedChanged;

                // If there's only one repository, pre-select it so releases load automatically.
                if (_repositories.Count == 1)
                    radio.Checked = true;

                _repoGrid.Controls.Add(radio, column, row);
            }
        }
        finally
        {
            _repoGrid.ResumeLayout(true);
        }
    }


    private Control BuildStatusSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(1),
            Margin = new Padding(0)
        };

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceMutedColor,
            Padding = new Padding(18, 0, 18, 0)
        };

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI Semibold", 10F);
        _statusLabel.ForeColor = TextPrimaryColor;
        _statusLabel.Text = "Ready.";
        inner.Controls.Add(_statusLabel);
        panel.Controls.Add(inner);

        return panel;
    }

    private async Task FetchRepositoriesAsync()
    {
        var busyReleased = false;

        try
        {
            if(string.IsNullOrEmpty(_ownerText.Text.Trim()))
            {
                ShowToast(Toast.ToastStatus.ERROR, "Owner Required", "Please enter a GitHub owner name.");
                return;
            }
            SetBusy(true, "Fetching repositories from GitHub...");
            _logger.Info($"Fetching repositories for {_ownerText.Text.Trim()}.");

            var limit = _config.MaxRepositoryResults;
            _repositories = await _github.GetRepositoriesAsync(_ownerText.Text.Trim(), maxResults: limit);
            PopulateRepositories();

            if (_repositories.Count == 0)
            {
                SetStatus("No repositories were found.", true);
            }
            else if (_repositories.Count >= limit)
            {
                var msg = $"Showing first {limit} repositories. Limit reached — increase MaxRepositoryResults in App.config to see more.";
                SetStatus(msg, true);
                ShowToast(Toast.ToastStatus.INFO, "Repository Limit Reached",
                    $"{limit} repositories loaded. Adjust MaxRepositoryResults in App.config to change this limit.");
            }
            else
            {
                SetStatus($"{_repositories.Count} repository/repositories loaded.", true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Repository retrieval failed.", ex);
            SetBusy(false);
            busyReleased = true;
            SetStatus($"Repository retrieval failed: {ex.Message}", false);
            ShowToast(Toast.ToastStatus.ERROR, "Fetch Failed", ex.Message);
        }
        finally
        {
            if (!busyReleased)
                SetBusy(false);
        }
    }

    private void PopulateRepositories()
    {
        _selectedRepository = null;
        ResetReleaseState();
        UpdateRepositoryGridLayout();
        UpdateDeployButton();
    }

    private async Task LoadReleasesAsync(GitHubRepository repository)
    {
        var busyReleased = false;

        try
        {
            SetBusy(true, $"Fetching releases for {repository.Name}...");
            ResetReleaseState();

            _releases = await _controller.LoadReleasesAsync(_github, _ownerText.Text.Trim(), repository.Name);

            // Highlight the repo if it has no releases
            if (_releases.Count == 0)
            {
                HighlightRepositoryWithoutReleases(repository);
            }

            PopulateReleases();

            SetStatus(_releases.Count == 0
                ? "The selected repository has no published releases."
                : $"{_releases.Count} release/release(s) loaded.", true);
            UpdateWorkflowVisibility();
        }
        catch (Exception ex)
        {
            _logger.Error($"Release retrieval failed for {repository.Name}.", ex);
            SetBusy(false);
            busyReleased = true;
            SetStatus($"Release retrieval failed: {ex.Message}", false);
        }
        finally
        {
            if (!busyReleased)
                SetBusy(false);
        }
    }

    private void HighlightRepositoryWithoutReleases(GitHubRepository repository)
    {
        if (_repoGrid is null)
            return;

        // Add to the set of repos without releases
        _repositoriesWithoutReleases.Add(repository.Name);

        foreach (Control control in _repoGrid.Controls)
        {
            if (control is not RadioButton radio || radio.Tag is not GitHubRepository repo)
                continue;

            if (_repositoriesWithoutReleases.Contains(repo.Name))
            {
                // Keep red highlighting for repos without releases
                radio.BackColor = Color.FromArgb(255, 239, 236); // Light red background
                radio.ForeColor = ErrorColor; // Keep text in error red
            }
            else
            {
                // Reset repos with releases to default styling
                radio.BackColor = Color.Transparent;
                radio.ForeColor = TextPrimaryColor;
            }
        }
    }

    private void PopulateReleases()
    {
        _releasePanel.Controls.Clear();
        _selectedRelease = null;
        _selectedPackage = null;
        _releaseStatus.Text = "";
        _packageTypeLabel.Text = "";
        _packageNotesLabel.Text = "";
        _assetLabel.Text = "";

        foreach (var release in _releases)
        {
            var published = (release.PublishedAt ?? release.CreatedAt)?.ToLocalTime().ToString("yyyy-MM-dd") ?? "Unknown date";
            var version = string.IsNullOrWhiteSpace(release.TagName) ? release.Name : release.TagName;
            var title = string.IsNullOrWhiteSpace(release.Name) || string.Equals(release.Name, version, StringComparison.OrdinalIgnoreCase)
                ? version
                : release.Name;
            var label = $"{title} ({published})";

            var radio = new RadioButton
            {
                Text = label,
                Tag = release,
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 8)
            };
            StyleOptionRadio(radio);

            EventHandler onReleaseChecked = async (_, _) =>
            {
                if (!radio.Checked) return;
                _selectedRelease = (GitHubRelease)radio.Tag;
                UpdateWorkflowVisibility();
                await ValidateSelectedReleaseAsync();
            };
            radio.CheckedChanged += onReleaseChecked;

            // If there's only one release, pre-select it so validation runs automatically.
            if (_releases.Count == 1)
                radio.Checked = true;

            _releasePanel.Controls.Add(radio);
        }

        UpdateDynamicSectionHeights();
        UpdateDeployButton();
    }

    private async Task ValidateSelectedReleaseAsync()
    {
        if (_selectedRepository is null || _selectedRelease is null) return;

        try
        {
            SetBusy(true, "Validating the selected release...");
            _selectedPackage = null;
            _releaseStatus.Text = "Checking release package...";
            _releaseStatus.ForeColor = WarningColor;
            _packageTypeLabel.Text = string.Empty;
            _packageNotesLabel.Text = string.Empty;
            _assetLabel.Text = string.Empty;
            UpdateDeployButton();

            var result = await _controller.ValidateReleaseAsync(
                _github,
                _ownerText.Text.Trim(),
                _selectedRepository.Name,
                _selectedRelease);

            _selectedPackage = result;
            _releaseStatus.Text = "✓ Release accessible";
            _releaseStatus.ForeColor = SuccessColor;
            _packageTypeLabel.Text = result.DisplayLabel;
            _packageNotesLabel.Text = result.Notes ?? string.Empty;
            _assetLabel.Text = result.Asset is not null
                ? $"Asset: {result.Asset.Name}"
                : $"Download: {result.Name}";
            SetStatus($"Release validation successful. {result.DisplayLabel}", true);
            UpdateWorkflowVisibility();
        }
        catch (Exception ex)
        {
            _logger.Error("Release validation failed.", ex);
            _releaseStatus.Text = "✕ Release unavailable";
            _releaseStatus.ForeColor = ErrorColor;
            _selectedPackage = null;
            _packageTypeLabel.Text = string.Empty;
            _packageNotesLabel.Text = string.Empty;
            _assetLabel.Text = string.Empty;
            SetStatus(ex.Message, false);
        }
        finally
        {
            SetBusy(false);
        }

        UpdateDeployButton();
    }

    private void BrowseTarget()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select deployment target folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_pathText.Text) ? _pathText.Text : ""
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _pathText.Text = dialog.SelectedPath;
    }

    private void ResetPathState()
    {
        _pathStatus.Text = string.Empty;
        _pathStatus.ForeColor = TextSecondaryColor;
        UpdateDeployButton();
    }

    private async Task ValidatePathAsync()
    {
        try
        {
            await _controller.ValidateTargetAsync(_pathText.Text.Trim());
            _pathStatus.Text = "✓ Deployment path accessible";
            _pathStatus.ForeColor = SuccessColor;
        }
        catch (Exception ex)
        {
            _pathStatus.Text = "✕ Deployment path unavailable";
            _pathStatus.ForeColor = ErrorColor;
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private async Task TestDeploymentPathAsync()
    {
        try
        {
            SetBusy(true, "Testing the deployment target connection...");
            _pathStatus.Text = "Checking deployment path...";
            _pathStatus.ForeColor = WarningColor;

            await ValidatePathAsync();
            SetStatus("", true);
           // ShowToast(Toast.ToastStatus.SUCCESS, "Path Validated", "The deployment target path is reachable and ready.");
        }
        catch (Exception ex)
        {
            _logger.Error("Deployment path validation failed.", ex);
            SetStatus(ex.Message, false);
            ShowToast(Toast.ToastStatus.ERROR, "Path Validation Failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
            UpdateDeployButton();
        }
    }

    private async Task DeployAsync()
    {
        if (_selectedRepository is null || _selectedRelease is null || _selectedPackage is null)
            return;

        try
        {
            _cancelledByUser = false;
            _operationCts = new CancellationTokenSource();
            SetBusy(true, "Validating deployment target...");
            SetDeploying(true);
            await ValidatePathAsync();

            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 15;
            _progress.Refresh();

            SetStatus($"Downloading { _selectedPackage.DisplayLabel }...", true);
            _logger.Info($"Deployment started. Repository={_selectedRepository.Name}, Release={_selectedRelease.TagName}, Package={_selectedPackage.Name}, Target={_pathText.Text}");

            var progress = new Progress<string>(message =>
            {
                _progress.Value = Math.Min(90, _progress.Value + 5);
                SetStatus(message, true);
            });
            var result = await _controller.DeployAsync(
                _github,
                _ownerText.Text.Trim(),
                _selectedRepository,
                _selectedRelease,
                _selectedPackage,
                _pathText.Text.Trim(),
                progress,
                _operationCts.Token);

            _progress.Value = 55;
            _progress.Refresh();
            SetStatus("Package downloaded. Preparing deployment...", true);

            _progress.Value = 90;
            _progress.Refresh();

            if (result.Success)
            {
                _progress.Value = 100;
                _progress.Refresh();
                Refresh();
                _logger.Info(result.Message);
                SetStatus("", true);
                ShowToast(Toast.ToastStatus.SUCCESS, "Deployment Successful",
                    $"{_selectedRelease.TagName} — {result.FilesCopied:N0} file(s) deployed to target.");
                SetBusy(false);
            }
            else
            {
                _logger.Error($"Deployment failed. {result.Message}");
                SetStatus($"✕ Deployment failed. {result.Message}", false);
                ShowToast(Toast.ToastStatus.ERROR, "Deployment Failed", result.Message);
                SetBusy(false);
            }
        }
        catch (OperationCanceledException)
        {
            if (_cancelledByUser)
            {
                _logger.Info("Deployment was forcibly cancelled by the user.");
                SetStatus("", false);
                ShowToast(Toast.ToastStatus.ERROR, "Deployment Cancelled",
                    "Deployment was forcibly cancelled by the user.");
            }
            else
            {
                SetStatus("Deployment cancelled.", false);
                ShowToast(Toast.ToastStatus.INFO, "Deployment Cancelled", "The deployment operation was cancelled.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Deployment failed unexpectedly.", ex);
            SetStatus($"✕ Deployment failed. {ex.Message}", false);
            ShowToast(Toast.ToastStatus.ERROR, "Deployment Error", ex.Message);
            SetBusy(false);
        }
        finally
        {
            _progress.Value = 0;
            _progress.Visible = false;
            _operationCts?.Dispose();
            _operationCts = null;
            SetDeploying(false);
            SetBusy(false);
        }
    }

    private void ResetReleaseState()
    {
        _releasePanel.Controls.Clear();
        _releases = [];
        _selectedRelease = null;
        _selectedPackage = null;
        _releaseStatus.Text = "";
        _assetLabel.Text = "";
        _packageTypeLabel.Text = "";
        _packageNotesLabel.Text = "";
        UpdateDynamicSectionHeights();
        UpdateDeployButton();
        UpdateWorkflowVisibility();
    }

    private async Task ResetAppAsync()
    {
        // Clear all selections and data (PAT remains unchanged)
        _statusLabel.Text = "";
        ResetRepositories();

        // Restore owner from configuration if present (do not auto-fetch)
        _ownerText.Text = string.IsNullOrWhiteSpace(_config.GitHubOwner) ? string.Empty : _config.GitHubOwner;

        // Restore default deployment path and validate it
        _pathText.Text = string.IsNullOrWhiteSpace(_config.DefaultDeploymentPath) ? string.Empty : _config.DefaultDeploymentPath;
        ResetPathState();
        await TestDeploymentPathAsync();

        // Show reset status as toast notification
        ShowToast(Toast.ToastStatus.INFO, "App Reset", "Selections cleared. Owner and deployment path restored from configuration.");

        UpdateWorkflowVisibility();
    }

    private void UpdateDeployButton()
    {
        var enabled =
            _selectedRepository is not null &&
            _selectedRelease is not null &&
            _selectedPackage is not null &&
            _pathStatus.Text.StartsWith("✓", StringComparison.Ordinal);

        _deployButton.Enabled = enabled;

        if (enabled)
        {
            // Primary (active) appearance
            StyleButton(_deployButton, primary: true);
            _deployButton.Cursor = Cursors.Hand;
        }
        else
        {
            // Disabled grey appearance
            _deployButton.FlatStyle = FlatStyle.Flat;
            _deployButton.FlatAppearance.BorderSize = 1;
            _deployButton.FlatAppearance.BorderColor = BorderColor;
            _deployButton.BackColor = Color.FromArgb(229, 231, 235); // light grey
            _deployButton.ForeColor = Color.FromArgb(107, 114, 128); // muted text
            _deployButton.Cursor = Cursors.Default;
        }
    }

    private void SetDeploying(bool deploying)
    {
        SaveFocusAndScroll();
        _cancelDeployButton.Visible = deploying;
        _deployButton.Visible = !deploying;
        RestoreFocusAndScroll();
    }

    private void SetBusy(bool busy, string? status = null)
    {
        // Preserve scroll position across transient busy state changes so focus shifts don't jump the view
        if (_scrollHost is not null)
        {
            var pos = _scrollHost.AutoScrollPosition;
            _savedScrollPosition = new Point(-pos.X, -pos.Y);
        }

        _applyPatButton.Enabled = !busy;
        _patText.Enabled = !busy;
        _fetchButton.Enabled = !busy;
        _ownerText.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _testPathButton.Enabled = !busy;
        _clearButton.Enabled = !busy;
        // Deploy button enabled state will be computed and styled by UpdateDeployButton()
        // (call it at the end of SetBusy so appearance updates consistently).
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

        if (busy)
        {
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 35;
            _progress.Value = 0;
        }
        else
        {
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            _progress.Visible = false;

            // Restore the scroll position when leaving busy state
            if (_savedScrollPosition != Point.Empty && _scrollHost is not null)
            {
                _scrollHost.AutoScrollPosition = _savedScrollPosition;
            }
        }

        if (status is not null) SetStatus(status, true);
        // Ensure deploy button appearance is correct after busy state changes.
        UpdateDeployButton();
    }

    private void SetStatus(string message, bool success)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = success ? TextPrimaryColor : ErrorColor;

        UpdateDeployButton();
    }

    private void ShowToast(Toast.ToastStatus status, string header, string message)
    {
        // Preserve scroll position when showing transient toast so closing it won't reset the view
        if (_scrollHost is not null)
        {
            var pos = _scrollHost.AutoScrollPosition;
            _savedScrollPosition = new Point(-pos.X, -pos.Y);
        }

        var toast = new Toast(this)
        {
            Position = Toast.ToastPosition.LOWER_RIGHT,
            Status = status,
            HeaderText = header,
            MessageText = message,
            Duration = status switch
            {
                Toast.ToastStatus.SUCCESS => Toast.ToastDuration.MEDIUM,
                Toast.ToastStatus.ERROR   => Toast.ToastDuration.LONG,
                _                        => Toast.ToastDuration.SHORT
            }
        };
        toast.ShowToast();
    }

    private void UpdateWorkflowVisibility()
    {
        // Preserve focus and scroll while updating visibility to avoid the view jumping.
        SaveFocusAndScroll();

        var hasReleases = _releases.Count > 0;
        var hasReleaseSelection = _selectedRelease is not null;

        if (_step2Card is not null)
            _step2Card.Visible = hasReleases;

        if (_step3Card is not null)
            _step3Card.Visible = hasReleaseSelection;

        if (_deploySection is not null)
            _deploySection.Visible = hasReleaseSelection;

        if (_statusSection is not null)
            _statusSection.Visible = true;

        RestoreFocusAndScroll();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // When the form regains activation (for example after a transient toast closes),
        // restore the previous scroll position so the UI doesn't jump back to the top.
        if (_scrollHost is not null && _savedScrollPosition != Point.Empty)
        {
            _scrollHost.AutoScrollPosition = _savedScrollPosition;
        }
    }

    private void SaveFocusAndScroll()
    {
        if (_scrollHost is not null)
        {
            var pos = _scrollHost.AutoScrollPosition;
            _savedScrollPosition = new Point(-pos.X, -pos.Y);
        }

        // Find the currently focused control within the form
        _savedFocusedControl = FindFocusedControl(this);
    }

    private void RestoreFocusAndScroll()
    {
        if (_scrollHost is not null && _savedScrollPosition != Point.Empty)
        {
            try { _scrollHost.AutoScrollPosition = _savedScrollPosition; } catch { }
        }

        if (_savedFocusedControl is not null && _savedFocusedControl.Visible && _savedFocusedControl.Enabled)
        {
            try { _savedFocusedControl.Focus(); } catch { }
        }

        // Clear saved focus - keep scroll saved until explicitly reset or top is requested
        _savedFocusedControl = null;
    }

    private Control? FindFocusedControl(Control parent)
    {
        if (parent is null) return null;
        if (parent.ContainsFocus) return parent;
        foreach (Control child in parent.Controls)
        {
            var focused = FindFocusedControl(child);
            if (focused is not null) return focused;
        }
        return null;
    }

    private void ScrollToTop()
    {
        if (_scrollHost is null)
            return;

        _scrollHost.AutoScrollPosition = Point.Empty;
        _scrollHost.VerticalScroll.Value = 0;
        _scrollHost.PerformLayout();
        _savedScrollPosition = Point.Empty; // reset saved position when explicitly scrolling to top
    }

    }
