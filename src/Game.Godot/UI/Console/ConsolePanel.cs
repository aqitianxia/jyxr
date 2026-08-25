using Game.Application;
using Godot;

namespace Game.Godot.UI;

public partial class ConsolePanel : JyPanel
{
	private const int MaxConsoleLineCount = 12;
	private readonly List<string> _consoleLines = [];
	private LineEdit _consoleInput = null!;
	private RichTextLabel _consoleOutput = null!;
	private Button _executeButton = null!;

	public override void _Ready()
	{
		base._Ready();

		_consoleInput = GetNode<LineEdit>("%ConsoleInput");
		_consoleOutput = GetNode<RichTextLabel>("%ConsoleOutput");
		_executeButton = GetNode<Button>("%ExecuteButton");
		_executeButton.Pressed += OnExecutePressed;
		_consoleInput.TextSubmitted += OnConsoleTextSubmitted;

		if (!Game.Config.ConsoleEnabled)
		{
			QueueFree();
			return;
		}

		AppendConsoleLine("系统", "命令行执行剧本指令，当前不支持 jump。");
		AppendConsoleLine("系统", "示例：item 道口烧鸡 / log \"踏入江湖\"");
		if (!Game.IsMobilePlatform)
		{
			_consoleInput.CallDeferred(Control.MethodName.GrabFocus);
		}
	}

	private void OnExecutePressed() => SubmitConsoleCommand(_consoleInput.Text);

	private void OnConsoleTextSubmitted(string text) => SubmitConsoleCommand(text);

	private async void SubmitConsoleCommand(string text)
	{
		var commandLine = text.Trim();
		if (string.IsNullOrWhiteSpace(commandLine))
		{
			AppendConsoleLine("控制台", "请输入有效指令。");
			return;
		}

		try
		{
			await Game.StoryService.CommandLine.ExecuteAsync(commandLine);
			_consoleInput.Clear();
			AppendConsoleLine("控制台", $"已执行剧本指令：{commandLine}");
		}
		catch (Exception exception)
		{
			Game.Logger.Error($"Console command failed: {commandLine}", exception);
			AppendConsoleLine("错误", exception.Message);
		}
	}

	private void AppendConsoleLine(string source, string message)
	{
		if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
		{
			return;
		}

		_consoleLines.Add($"[color=#513523]{source}[/color]  {message}");
		while (_consoleLines.Count > MaxConsoleLineCount)
		{
			_consoleLines.RemoveAt(0);
		}

		_consoleOutput.Clear();
		foreach (var line in _consoleLines)
		{
			_consoleOutput.AppendText(line + "\n");
		}
	}
}
