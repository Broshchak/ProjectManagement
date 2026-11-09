using System.Windows;
using Orders.Workflow;
using WpfClient.Models;

namespace WpfClient.Views;

public partial class KanbanMoveWindow : Window
{
    public KanbanMoveWindow(string destination, IReadOnlyList<WorkflowAction> actions)
    {
        Destination = destination;
        Actions = actions;
        InitializeComponent();
        DataContext = this;
        ActionPicker.SelectedIndex = actions.Count == 1 ? 0 : -1;
        InputChanged(this, new RoutedEventArgs());
        Loaded += (_, _) =>
        {
            if (SelectedAction?.RequiresComment == true) CommentInput.Focus();
            else ActionPicker.Focus();
        };
    }

    public LocalizationManager T => LocalizationManager.Instance;
    public string Destination { get; }
    public IReadOnlyList<WorkflowAction> Actions { get; }
    public WorkflowAction? SelectedAction => ActionPicker.SelectedItem as WorkflowAction;
    public string? Comment => string.IsNullOrWhiteSpace(CommentInput.Text) ? null : CommentInput.Text.Trim();

    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (ConfirmButton is null) return;
        CommentLabel.Text = T[SelectedAction?.RequiresComment == true ? "KanbanRequiredComment" : "KanbanOptionalComment"];
        ConfirmButton.IsEnabled = SelectedAction is { } action && (!action.RequiresComment || Comment is not null);
    }

    private void ConfirmClicked(object sender, RoutedEventArgs e) => DialogResult = true;
}
