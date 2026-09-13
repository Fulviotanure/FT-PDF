using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FtPdf
{
    /// <summary>
    /// GridLengthAnimation customizada para animar suavemente a largura de ColumnDefinitions em WPF.
    /// </summary>
    public class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction? EasingFunction
        {
            get => (IEasingFunction?)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double from = ((GridLength)GetValue(FromProperty)).Value;
            double to = ((GridLength)GetValue(ToProperty)).Value;
            double progress = animationClock.CurrentProgress ?? 0.0;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);
            double current = from + (to - from) * progress;
            return new GridLength(current);
        }
    }
}