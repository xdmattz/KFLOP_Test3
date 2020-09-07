using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit;

namespace KFLOP_Test3
{
    // line highligher - found this at 
    // https://stackoverflow.com/questions/5072761/avalonedit-highlight-current-line-even-when-not-focused

    public class HighlightCurrentLineBackgroundRenderer : IBackgroundRenderer
    {
        private TextEditor _editor;

        public HighlightCurrentLineBackgroundRenderer(TextEditor editor)
        {
            _editor = editor;
        }

        public KnownLayer Layer
        {
            get { return KnownLayer.Background; }
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null)
                return;

            textView.EnsureVisualLines();
            var currentLine = _editor.Document.GetLineByOffset(_editor.CaretOffset);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, currentLine))
            {
                drawingContext.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0xFF)), null,
                    // the next line works, but leaves a small gap to on the right side of the window - the following line fixes it. see comments in the reference above
                    //new Rect(rect.Location, new Size(textView.ActualWidth - 32, rect.Height)));
                    new Rect(new Point(rect.Location.X + textView.ScrollOffset.X, rect.Location.Y), new Size(textView.ActualWidth, rect.Height)));
            }
        }
    }
}
