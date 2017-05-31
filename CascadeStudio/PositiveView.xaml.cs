﻿namespace CascadeStudio
{
    using System.Windows.Controls;
    using System.Windows.Input;
    using Image = System.Windows.Controls.Image;
    using Point = System.Windows.Point;

    public partial class PositiveView : UserControl
    {
        private Button dragged;
        private Point position;

        public PositiveView()
        {
            this.InitializeComponent();
        }

        private PositiveViewModel ViewModel => (PositiveViewModel)this.DataContext;

        private void OnAdd(object _, ExecutedRoutedEventArgs e)
        {
            var image = (Image)e.OriginalSource;
            var p = Mouse.GetPosition(image);
            var w = this.ViewModel.Width;
            var h = this.ViewModel.Height;
            var rectangle = new RectangleInfo((int)p.X - (w / 2), (int)(p.Y - (h / 2)), w, h);
            this.ViewModel.Rectangles.Add(rectangle);
            e.Handled = true;
        }

        private void Rectangle_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                e.Delta != 0)
            {
                var rectangle = (RectangleInfo)((Button)sender).DataContext;
                if (e.Delta > 0)
                {
                    rectangle.IncreaseSizeCommand.Execute(null);
                }
                else
                {
                    rectangle.DecreaseSizeCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var button = (Button)sender;
            this.position = e.GetPosition(this.Image);
            this.dragged = button;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var button = (Button)sender;
            if (ReferenceEquals(button, this.dragged))
            {
                var rectangle = (RectangleInfo)button.DataContext;
                var pos = e.GetPosition(this.Image);
                var delta = pos - this.position;
                if (delta.LengthSquared < 2)
                {
                    return;
                }

                rectangle.X += (int)delta.X;
                rectangle.Y += (int)delta.Y;
                this.position = pos;
            }

            if (e.LeftButton == MouseButtonState.Released)
            {
                this.dragged = null;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.dragged = null;
        }
    }
}
