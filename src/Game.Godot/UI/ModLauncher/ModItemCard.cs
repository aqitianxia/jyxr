using Game.Application.Mods;
using Godot;

namespace Game.Godot.UI.ModLauncher;

public partial class ModItemCard : PanelContainer
{
	private static readonly string[] PosterFileNames =
	[
		"poster.png",
		"poster.jpg",
		"poster.jpeg",
		"poster.webp",
	];

	private ModContext? _context;
	private Control _posterFrame = null!;
	private TextureRect _poster = null!;
	private Label _nameLabel = null!;
	private Label _descriptionLabel = null!;
	private Button _primarySelectionButton = null!;
	private CheckBox _addonSelectionCheckBox = null!;
	private Button _moveUpButton = null!;
	private Button _moveDownButton = null!;
	private bool _isGame;
	private bool _configuring;

	public event Action<ModContext>? PrimarySelected;
	public event Action<ModContext, bool>? AddonToggled;
	public event Action<ModContext, int>? MoveRequested;

	public override void _Ready()
	{
		_posterFrame = GetNode<Control>("%PosterFrame");
		_poster = GetNode<TextureRect>("%Poster");
		_nameLabel = GetNode<Label>("%NameLabel");
		_descriptionLabel = GetNode<Label>("%DescriptionLabel");
		_primarySelectionButton = GetNode<Button>("%PrimarySelectionButton");
		_addonSelectionCheckBox = GetNode<CheckBox>("%AddonSelectionCheckBox");
		_moveUpButton = GetNode<Button>("%MoveUpButton");
		_moveDownButton = GetNode<Button>("%MoveDownButton");

		_primarySelectionButton.Toggled += OnPrimarySelectionToggled;
		_addonSelectionCheckBox.Toggled += OnAddonSelectionToggled;
		_moveUpButton.Pressed += () => RequestMove(-1);
		_moveDownButton.Pressed += () => RequestMove(1);
	}

	public void Configure(
		ModContext context,
		bool isPrimary,
		bool isEnabled,
		bool canMoveUp,
		bool canMoveDown)
	{
		ArgumentNullException.ThrowIfNull(context);
		_configuring = true;
		_context = context;
		_nameLabel.Text = context.Manifest.Name;
		_descriptionLabel.Text = FormatDescription(context.Manifest);
		var posterPath = FindPosterPath(context.ModDirectoryPath);
		var posterTexture = posterPath is null ? null : LoadPosterTexture(posterPath);
		_posterFrame.Visible = posterTexture is not null;
		_poster.Texture = posterTexture;
		_poster.TooltipText = posterTexture is null ? string.Empty : posterPath!;

		_isGame = context.Manifest.Type == ModType.Game;
		var isSelected = _isGame ? isPrimary : isEnabled;
		_primarySelectionButton.Visible = _isGame;
		_primarySelectionButton.ButtonPressed = _isGame && isSelected;
		_addonSelectionCheckBox.Visible = !_isGame;
		_addonSelectionCheckBox.ButtonPressed = !_isGame && isSelected;
		_moveUpButton.Visible = !_isGame && isEnabled;
		_moveDownButton.Visible = !_isGame && isEnabled;
		_moveUpButton.Disabled = !canMoveUp;
		_moveDownButton.Disabled = !canMoveDown;
		_configuring = false;
	}

	private void OnPrimarySelectionToggled(bool selected)
	{
		if (_configuring || _context is null || !_isGame)
		{
			return;
		}

		if (selected)
		{
			PrimarySelected?.Invoke(_context);
			return;
		}

		_configuring = true;
		_primarySelectionButton.ButtonPressed = true;
		_configuring = false;
	}

	private void OnAddonSelectionToggled(bool selected)
	{
		if (_configuring || _context is null || _isGame)
		{
			return;
		}

		AddonToggled?.Invoke(_context, selected);
	}

	private void RequestMove(int offset)
	{
		if (_context is not null)
		{
			MoveRequested?.Invoke(_context, offset);
		}
	}

	private static string FormatDescription(ModManifest manifest)
	{
		var parts = new List<string>();
		var metaParts = new List<string>();
		if (!string.IsNullOrWhiteSpace(manifest.Author))
		{
			metaParts.Add($"作者：{manifest.Author.Trim()}");
		}

		metaParts.Add($"版本：{manifest.Version.Trim()}");
		metaParts.Add($"存档影响：{FormatSaveImpact(manifest.SaveImpact)}");
		if (!string.IsNullOrWhiteSpace(manifest.Date))
		{
			metaParts.Add($"时间：{manifest.Date.Trim()}");
		}

		parts.Add(string.Join("  ", metaParts));
		if (manifest.ResolvedDependencies.Count > 0)
		{
			parts.Add($"依赖：{string.Join("、", manifest.ResolvedDependencies)}");
		}

		if (!string.IsNullOrWhiteSpace(manifest.Description))
		{
			parts.Add(manifest.Description.Trim());
		}

		return string.Join("\n", parts);
	}

	private static string FormatSaveImpact(SaveImpact impact) => impact switch
	{
		SaveImpact.None => "无",
		SaveImpact.Gameplay => "玩法",
		SaveImpact.Structural => "结构",
		_ => throw new ArgumentOutOfRangeException(nameof(impact), impact, "Unsupported save impact."),
	};

	private static string? FindPosterPath(string modDirectoryPath)
	{
		foreach (var fileName in PosterFileNames)
		{
			var posterPath = Path.Combine(modDirectoryPath, fileName);
			if (File.Exists(posterPath))
			{
				return posterPath;
			}
		}

		return null;
	}

	private static Texture2D? LoadPosterTexture(string posterPath)
	{
		var image = new Image();
		var error = image.Load(posterPath);
		if (error != Error.Ok)
		{
			GD.PushWarning($"Failed to load mod poster '{posterPath}': {error}");
			return null;
		}

		return ImageTexture.CreateFromImage(image);
	}
}
