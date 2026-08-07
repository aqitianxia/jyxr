using Game.Application;
using Game.Core.Definitions;
using Game.Core.Model;
using Game.Core.Model.Character;
using Godot;

namespace Game.Godot.UI;

public partial class ItemTargetSelectionPanel : JyPanel
{
	[Export]
	public PackedScene TargetCharacterBoxScene { get; set; } = null!;

	private GridContainer _gridContainer = null!;
	private Label _itemLabel = null!;
	private Label _hintLabel = null!;
	private InventoryEntry? _entry;
	private IDisposable? _saveLoadedSubscription;
	private bool _isUsing;

	public override void _Ready()
	{
		base._Ready();
		_gridContainer = GetNode<GridContainer>("%GridContainer");
		_itemLabel = GetNode<Label>("%ItemLabel");
		_hintLabel = GetNode<Label>("%HintLabel");
		_saveLoadedSubscription = Game.Session.Events.Subscribe<SaveLoadedEvent>(_ => QueueFree());
		Refresh();
	}

	public override void _ExitTree()
	{
		_saveLoadedSubscription?.Dispose();
		_saveLoadedSubscription = null;
		base._ExitTree();
	}

	public void Configure(InventoryEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		_entry = entry;
		Refresh();
	}

	private void Refresh()
	{
		if (!IsInsideTree() || _entry is null)
		{
			return;
		}

		ClearGrid();
		var analysis = Game.ItemUseService.Analyze(_entry);
		_itemLabel.Text = $"选择目标：{_entry.Definition.Name}";
		_hintLabel.Text = analysis.Message;

		foreach (var candidate in analysis.Targets)
		{
			var character = Game.State.Party.GetMember(candidate.CharacterId);
			var box = CreateCharacterBox(character, candidate);
			_gridContainer.AddChild(box);
		}
	}

	private ItemTargetCharacterBox CreateCharacterBox(
		CharacterInstance character,
		ItemUseTargetCandidate candidate)
	{
		if (TargetCharacterBoxScene is null)
		{
			throw new InvalidOperationException("TargetCharacterBoxScene is not assigned.");
		}

		var instance = TargetCharacterBoxScene.Instantiate();
		if (instance is not ItemTargetCharacterBox box)
		{
			instance.QueueFree();
			throw new InvalidOperationException("TargetCharacterBox scene root must be ItemTargetCharacterBox.");
		}

		box.Setup(character, candidate);
		box.TargetSelected += OnTargetSelected;
		return box;
	}

	private async void OnTargetSelected(string characterId)
	{
		if (_entry is null || _isUsing)
		{
			return;
		}

		_isUsing = true;
		var entry = _entry;
		var runsStory = entry.Definition.UseEffects is [RunStoryItemUseEffectDefinition];
		if (runsStory)
		{
			UIRoot.Instance.CloseMainPanel();
			UIRoot.Instance.SetStoryPresentationActive(true);
			QueueFree();
		}

		try
		{
			var result = await Game.ItemUseService.UseAsync(entry, characterId);
			if (!result.Success)
			{
				UIRoot.Instance.ShowSuggestion(result.Message);
				return;
			}

			if (!result.Message.IsWhiteSpace())
			{
				UIRoot.Instance.ShowToast(result.Message);
			}
			if (!runsStory)
			{
				QueueFree();
			}
		}
		catch (Exception exception)
		{
			Game.Logger.Error("Using inventory item failed.", exception);
			UIRoot.Instance.ShowSuggestion(exception.Message);
		}
		finally
		{
			if (runsStory && GodotObject.IsInstanceValid(UIRoot.Instance))
			{
				UIRoot.Instance.SetStoryPresentationActive(false);
			}
			_isUsing = false;
		}
	}

	private void ClearGrid()
	{
		foreach (var child in _gridContainer.GetChildren())
		{
			child.QueueFree();
		}
	}
}
