using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfClient.Models;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class KanbanView : UserControl
{
    private const string DragFormat = "Orders.KanbanMove";
    private FrameworkElement? _pressedCard;
    private Point _pressPosition;
    private int _gesture;
    private KanbanMove? _drag;
    private KanbanColumn? _droppedColumn;
    private long _lastScroll;

    public KanbanView()
    {
        InitializeComponent();
        Unloaded += (_, _) => CancelPress();
    }

    private void BoardMousePressed(object sender, MouseButtonEventArgs e) => CancelPress();
    private void BoardMouseReleased(object sender, MouseButtonEventArgs e) => CancelPress();

    private void CancelPress()
    {
        _gesture++;
        _pressedCard = null;
    }

    private void CardMousePressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OrderListItem order } card ||
            DataContext is not MainViewModel vm || !vm.CanPrepareKanbanMove(order) || FromInteractiveControl(e.OriginalSource, card))
            return;
        _pressedCard = card;
        _pressPosition = e.GetPosition(this);
    }

    private async void BoardMouseMoved(object sender, MouseEventArgs e)
    {
        if (_drag is not null || e.LeftButton != MouseButtonState.Pressed ||
            _pressedCard is not { DataContext: OrderListItem order } card || DataContext is not MainViewModel vm)
            return;
        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _pressPosition.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _pressPosition.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        _pressedCard = null;
        int gesture = _gesture;
        var move = await vm.PrepareKanbanMoveAsync(order);
        if (move is null) return;

        try
        {
            if (gesture != _gesture || !IsLoaded || !IsMouseOver || Mouse.LeftButton != MouseButtonState.Pressed ||
                !ReferenceEquals(DataContext, vm) || !move.Actions.Any(action => vm.CanDropKanbanMove(move, action.TargetStatus)))
                return;
            _drag = move;
            _droppedColumn = null;
            var highlighted = vm.KanbanColumns.Where(column => vm.CanDropKanbanMove(move, column.Status)).ToArray();
            foreach (var column in highlighted) column.IsDropTarget = true;
            DragDropEffects result;
            try { result = DragDrop.DoDragDrop(card, new DataObject(DragFormat, "1"), DragDropEffects.Move); }
            finally
            {
                foreach (var column in highlighted) column.IsDropTarget = false;
                _drag = null;
            }

            if (result != DragDropEffects.Move || _droppedColumn is not { } target || !vm.CanDropKanbanMove(move, target.Status)) return;
            var actions = move.Actions.Where(action => action.TargetStatus == target.Status).ToArray();
            var action = actions[0];
            string? comment = null;
            if (actions.Length > 1 || action.RequiresComment)
            {
                var dialog = new KanbanMoveWindow(target.Title, actions) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true || dialog.SelectedAction is not { } chosen) return;
                action = chosen;
                comment = dialog.Comment;
            }

            await vm.ExecuteKanbanMoveAsync(move, target.Status, action, comment);
        }
        finally
        {
            vm.CancelKanbanMove(move);
        }
    }

    private void ColumnDragOver(object sender, DragEventArgs e) =>
        SetDropEffect(sender, e, dropped: false);

    private void ColumnDropped(object sender, DragEventArgs e) =>
        SetDropEffect(sender, e, dropped: true);

    private void SetDropEffect(object sender, DragEventArgs e, bool dropped)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (_drag is not { } move || !e.Data.GetDataPresent(DragFormat, autoConvert: false) ||
            sender is not FrameworkElement { DataContext: KanbanColumn { IsDropTarget: true } column } ||
            DataContext is not MainViewModel vm || !vm.CanDropKanbanMove(move, column.Status))
            return;
        e.Effects = DragDropEffects.Move;
        if (dropped) _droppedColumn = column;
    }

    private void BoardDragOver(object sender, DragEventArgs e)
    {
        if (_drag is null || sender is not ScrollViewer viewer || viewer.ScrollableWidth <= 0) return;
        var x = e.GetPosition(viewer).X;
        int direction = x < 36 ? -1 : x > viewer.ViewportWidth - 36 ? 1 : 0;
        long now = Environment.TickCount64;
        if (direction != 0 && now - _lastScroll >= 60)
        {
            _lastScroll = now;
            viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset + direction * 34);
        }
    }

    private static bool FromInteractiveControl(object source, FrameworkElement card)
    {
        for (var element = source as DependencyObject; element is not null && element != card;)
        {
            if (element is ButtonBase or TextBoxBase or Selector or Thumb) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}
