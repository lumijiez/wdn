using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shapes;
using NAudio.Wave;
using NAudio.Dsp;
using System.Diagnostics;
using System.Windows.Controls;
using WDN.Models;

namespace WDN;

public partial class MainWindow
{
    private readonly DispatcherTimer _timer;
    private WasapiLoopbackCapture? _capture;
    private const int FftLength = 1024;
    private readonly Rectangle[] _bars;
    private const int BarCount = 10;
    private const double BarWidth = 4;
    private const double BarSpacing = 2;
    private readonly Queue<float> _audioBuffer;
    private readonly Random _random = new();
    private readonly List<Particle> _particles = [];
    private readonly DispatcherTimer _particleTimer;
    private double _currentAudioLevel;
    private readonly Queue<double> _recentLevels = new(30); 
    // private const double BeatDropThreshold = 0.65; 
    private const int BeatDropParticleCount = 50;
    // private DateTime _lastBeatDrop = DateTime.MinValue;
    // private const double BeatDropCooldown = 0.2;

    public MainWindow()
    {
        InitializeComponent();
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        _audioBuffer = new Queue<float>(FftLength);

        _bars = new Rectangle[BarCount];
        for (var i = 0; i < BarCount; i++)
        {
            var progress = i / (double)(BarCount - 1);
            var color = new Color
            {
                R = 255, 
                G = (byte)(165 * (1 - progress)), 
                B = 0,
                A = 255
            };

            var rect = new Rectangle
            {
                Width = BarWidth,
                Height = 2,
                Fill = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Canvas.SetLeft(rect, i * (BarWidth + BarSpacing));
            VisualizerCanvas.Children.Add(rect);
            _bars[i] = rect;
        }
        
        InitializeAudioCapture();

        var debugTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        debugTimer.Tick += (_, _) => Debug.WriteLine($"Audio buffer size: {_audioBuffer.Count}");
        debugTimer.Start();
        
        _particleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(7) 
        };
        _particleTimer.Tick += ParticleTimer_Tick;
        _particleTimer.Start();
    }
    
    private void ParticleTimer_Tick(object? sender, EventArgs e)
    {
        // UpdateParticles();
        UpdateGlowEffect();
    }
    
    private static Color HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = h / 60 - Math.Floor(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        var (r, g, b) = hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return Color.FromRgb(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }

    private void UpdateGlowEffect()
{
    var intensity = Math.Min(_currentAudioLevel * 2, 1.0);
    var breathe = (Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds) + 1) / 2;
    var combinedIntensity = (intensity * 0.7 + breathe * 0.3);
    
    var hue = (DateTime.Now.TimeOfDay.TotalMilliseconds / 20.0 + (_currentAudioLevel * 180)) % 360;
    var color = HsvToRgb(hue, 1, 1);
    
    GlowShadow.Color = color;
    GlowShadow.BlurRadius = 20 + (combinedIntensity * 10);
    GlowShadow.Opacity = 0.3 + (combinedIntensity * 0.7);
    
    var midColor = Color.FromRgb(
        (byte)(17 + (combinedIntensity * 40)),
        (byte)(17 + (combinedIntensity * 30)),
        (byte)(17 + (combinedIntensity * 50)));
    GradientMidPoint.Color = Color.FromArgb(204, midColor.R, midColor.G, midColor.B);
}

 // private void UpdateParticles()
 //    {
 //        _recentLevels.Enqueue(_currentAudioLevel);
 //        if (_recentLevels.Count > 30) 
 //        {
 //            _recentLevels.Dequeue();
 //        }
 //
 //        var averageLevel = _recentLevels.Count > 0 ? _recentLevels.Average() : 0;
 //        // var relativeIncrease = averageLevel > 0 ? (_currentAudioLevel - averageLevel) / averageLevel : 0;
 //        
 //        // var timeSinceLastBeatDrop = (DateTime.Now - _lastBeatDrop).TotalSeconds;
 //        //
 //        // if (relativeIncrease > BeatDropThreshold && timeSinceLastBeatDrop > BeatDropCooldown && _currentAudioLevel > 0.1)
 //        // {
 //        //     var intensity = Math.Min(relativeIncrease * 2, 1.0);
 //        //     CreateBeatDropEffect(intensity);
 //        //     _lastBeatDrop = DateTime.Now;
 //        // }
 //        
 //        var avgBarHeight = _bars.Average(bar => bar.Height);
 //        var normalizedHeight = Math.Min(avgBarHeight / 20.0, 1.0);
 //        
 //        for (var i = _particles.Count - 1; i >= 0; i--)
 //        {
 //            var particle = _particles[i];
 //            
 //            var velocityScale = 0.5 + normalizedHeight;
 //            particle.VelocityX *= velocityScale;
 //            particle.VelocityY *= velocityScale;
 //            
 //            particle.X += particle.VelocityX;
 //            particle.Y += particle.VelocityY;
 //            particle.VelocityY += 0.1 * velocityScale;
 //            
 //            var age = (0.8 - particle.Opacity) / 0.02;
 //            particle.VelocityX += Math.Sin(age * 0.1) * 0.05 * normalizedHeight;
 //            
 //            particle.Opacity -= 0.02;
 //            
 //            if (particle.Opacity <= 0)
 //            {
 //                ParticleCanvas.Children.Remove(particle.Shape);
 //                _particles.RemoveAt(i);
 //                continue;
 //            }
 //
 //            particle.Size += 0.05;
 //            switch (particle.Shape)
 //            {
 //                case Ellipse ellipse:
 //                    ellipse.Width = particle.Size;
 //                    ellipse.Height = particle.Size;
 //                    break;
 //                case null:
 //                    continue;
 //            }
 //
 //            Canvas.SetLeft(particle.Shape, particle.X);
 //            Canvas.SetTop(particle.Shape, particle.Y);
 //            particle.Shape.Opacity = particle.Opacity;
 //
 //            if (particle.Shape is not { } shape) continue;
 //            var hue = (DateTime.Now.TimeOfDay.TotalMilliseconds / 10.0 + (particle.Y * 0.5)) % 360;
 //            shape.Fill = new SolidColorBrush(HsvToRgb(hue, 1, 1));
 //        }
 //
 //        if (!(_currentAudioLevel > 0.05)) return;
 //        {
 //            var particleCount = (int)(_currentAudioLevel * 5);
 //            var maxBarHeight = _bars.Max(bar => bar.Height);
 //            var velocityScale = Math.Min(maxBarHeight / 20.0, 1.0) * 2 + 0.5;
 //            
 //            for (var i = 0; i < particleCount; i++)
 //            {
 //                AddParticle(velocityScale);
 //            }
 //        }
 //    }

    // private void CreateBeatDropEffect(double intensity)
    // {
    //     var particleCount = (int)(BeatDropParticleCount * (0.7 + (intensity * 0.3)));
    //     
    //     for (var i = 0; i < particleCount; i++)
    //     {
    //         var angle = (Math.PI * 2 * i) / particleCount;
    //         var speed = (5 + _random.NextDouble() * 5) * (0.8 + (intensity * 0.4));
    //         
    //         var particle = new Particle
    //         {
    //             X = VisualizerCanvas.ActualWidth / 2,
    //             Y = VisualizerCanvas.ActualHeight,
    //             VelocityX = Math.Cos(angle) * speed,
    //             VelocityY = Math.Sin(angle) * speed - (2 * intensity), 
    //             Size = (_random.NextDouble() * 3 + 2) * (0.8 + (intensity * 0.4)),
    //             Opacity = 0.8 + (intensity * 0.2) 
    //         };
    //
    //         Shape shape;
    //         if (_random.NextDouble() > (0.7 - (intensity * 0.4)))
    //         {
    //             shape = CreateStar(particle.Size);
    //         }
    //         else
    //         {
    //             shape = new Ellipse
    //             {
    //                 Width = particle.Size,
    //                 Height = particle.Size,
    //             };
    //         }
    //
    //         var hue = _random.NextDouble() * 360;
    //         var color = HsvToRgb(hue, 1, 1);
    //         shape.Fill = new SolidColorBrush(color);
    //
    //         Canvas.SetLeft(shape, particle.X);
    //         Canvas.SetTop(shape, particle.Y);
    //         
    //         particle.Shape = shape;
    //         _particles.Add(particle);
    //         ParticleCanvas.Children.Add(shape);
    //     }
    // }

// private void AddParticle(double velocityScale)
// {
//     var x = _random.NextDouble() * VisualizerCanvas.ActualWidth;
//     var y = VisualizerCanvas.ActualHeight;
//
//     var particle = new Particle
//     {
//         X = x,
//         Y = y,
//         VelocityX = (_random.NextDouble() - 0.5) * 3 * velocityScale,
//         VelocityY = -_random.NextDouble() * 7 * velocityScale,
//         Size = _random.NextDouble() * 2 + 1,
//         Opacity = 0.8
//     };
//
//     Shape shape;
//     if (_random.NextDouble() > 0.7)
//     {
//         shape = CreateStar(particle.Size);
//     }
//     else
//     {
//         shape = new Ellipse
//         {
//             Width = particle.Size,
//             Height = particle.Size,
//         };
//     }
//
//     var hue = (DateTime.Now.TimeOfDay.TotalMilliseconds / 10.0 + (particle.Y * 0.5)) % 360;
//     shape.Fill = new SolidColorBrush(HsvToRgb(hue, 1, 1));
//
//     Canvas.SetLeft(shape, particle.X);
//     Canvas.SetTop(shape, particle.Y);
//     
//     particle.Shape = shape;
//     _particles.Add(particle);
//     ParticleCanvas.Children.Add(shape);
// }

// private static Path CreateStar(double size)
// {
//     var points = new List<Point>();
//     const int numPoints = 5;
//     for (var i = 0; i < numPoints * 2; i++)
//     {
//         var radius = i % 2 == 0 ? size : size / 2;
//         var angle = i * Math.PI / numPoints;
//         points.Add(new Point(
//             radius * Math.Cos(angle),
//             radius * Math.Sin(angle)
//         ));
//     }
//
//     var figure = new PathFigure
//     {
//         StartPoint = points[0],
//         IsClosed = true
//     };
//
//     for (var i = 1; i < points.Count; i++)
//     {
//         figure.Segments.Add(new LineSegment(points[i], true));
//     }
//
//     var geometry = new PathGeometry();
//     geometry.Figures.Add(figure);
//
//     return new Path
//     {
//         Data = geometry,
//         RenderTransform = new TranslateTransform(-size, -size)
//     };
// }

    private void InitializeAudioCapture()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            
            _capture.DataAvailable += (_, e) =>
            {
                var samples = new float[e.BytesRecorded / 4];
                Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

                foreach (var sample in samples)
                {
                    _audioBuffer.Enqueue(sample);
                    if (_audioBuffer.Count > FftLength)
                        _audioBuffer.Dequeue();
                }

                if (_audioBuffer.Count >= FftLength)
                {
                    ProcessAudio();
                }
            };

            _capture.RecordingStopped += (_, _) =>
            {
                Debug.WriteLine("Recording stopped");
                try
                {
                    _capture.StartRecording();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to restart recording: {ex.Message}");
                }
            };

            _capture.StartRecording();
            Debug.WriteLine("Audio capture started successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing audio capture: {ex.Message}");
            MessageBox.Show($"Error initializing audio capture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ProcessAudio()
{
    try
    {
        var complex = new Complex[FftLength];
        var buffer = _audioBuffer.ToArray();
        
        for (var i = 0; i < FftLength; i++)
        {
            complex[i].X = buffer[i] * (float)FastFourierTransform.HammingWindow(i, FftLength);
            complex[i].Y = 0.0f;
        }

        FastFourierTransform.FFT(true, 10, complex);

        var bands = new double[BarCount];
        if (_capture != null)
        {
            var maxFrequency = _capture.WaveFormat.SampleRate / 2;
            const int minFrequency = 20;

            var frequencies = new double[BarCount + 1];
            for (var i = 0; i <= BarCount; i++)
            {
                frequencies[i] = minFrequency * Math.Pow((double) maxFrequency / minFrequency, i / (double)BarCount);
            }

            for (var i = 0; i < BarCount; i++)
            {
                var lowFreq = frequencies[i];
                var highFreq = frequencies[i + 1];
            
                var binLow = (int)Math.Floor(lowFreq * FftLength / _capture.WaveFormat.SampleRate);
                var binHigh = (int)Math.Ceiling(highFreq * FftLength / _capture.WaveFormat.SampleRate);
            
                binLow = Math.Max(0, Math.Min(binLow, FftLength / 2 - 1));
                binHigh = Math.Max(0, Math.Min(binHigh, FftLength / 2));

                var sum = 0.0;
                var count = 0;

                for (var j = binLow; j < binHigh; j++)
                {
                    var magnitude = Math.Sqrt(complex[j].X * complex[j].X + complex[j].Y * complex[j].Y);

                    var weight = i switch
                    {
                        < BarCount / 3 => 0.3 + i * 0.5 / (BarCount / (double) 3),
                        < 2 * BarCount / 3 => 2,
                        _ => 10
                    };

                    sum += magnitude * weight;
                    count++;
                }

                bands[i] = count > 0 ? sum / count : 0;
            }
        }

        const double smoothing = 0.3;
        static double Smooth(double current, double previous) => smoothing * previous + (1 - smoothing) * current;

        Dispatcher.Invoke(() =>
        {
            for (var i = 0; i < BarCount; i++)
            {
                var scaleFactor = 1000 * (0.7 + (i * 0.3 / BarCount));  
                var height = Math.Min(bands[i] * scaleFactor, 20);
                
                var currentHeight = _bars[i].Height;
                var smoothedHeight = Math.Max(2, Smooth(height, currentHeight));

                var animation = new DoubleAnimation
                {
                    To = smoothedHeight,
                    Duration = TimeSpan.FromMilliseconds(30)
                };
                _bars[i].BeginAnimation(HeightProperty, animation);
            }
            var sum = bands.Sum();
            _currentAudioLevel = Math.Min(sum / (BarCount * 0.03), 1.0);
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error processing audio: {ex.Message}");
    }
}

    private void Timer_Tick(object? sender, EventArgs e)
    {
        TimeDisplay.Text = DateTime.Now.ToString("HH:mm");
    }

    private void Notch_MouseEnter(object sender, MouseEventArgs e)
    {
        MusicControls.Visibility = Visibility.Visible;
    
        MusicControls.UpdateLayout();
    
        var expandedWidth = TimeDisplay.ActualWidth + 
                            VisualizerCanvas.ActualWidth + 
                            MusicControls.ActualWidth +
                            40; 
    
        var expandAnimation = new DoubleAnimation
        {
            To = expandedWidth,
            Duration = new Duration(TimeSpan.FromSeconds(0.2))
        };
        Notch.BeginAnimation(WidthProperty, expandAnimation);
    }

    private void Notch_MouseLeave(object sender, MouseEventArgs e)
    {
        var collapsedWidth = TimeDisplay.ActualWidth + 
                             VisualizerCanvas.ActualWidth +
                             20; 
    
        var shrinkAnimation = new DoubleAnimation
        {
            To = collapsedWidth,
            Duration = new Duration(TimeSpan.FromSeconds(0.2))
        };
        shrinkAnimation.Completed += (_, _) =>
        {
            MusicControls.Visibility = Visibility.Collapsed;
        };
    
        Notch.BeginAnimation(WidthProperty, shrinkAnimation);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var initialWidth = TimeDisplay.ActualWidth + 
                           VisualizerCanvas.ActualWidth +
                           20; 
    
        Notch.Width = initialWidth;
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;

        Notch.MouseEnter += Notch_MouseEnter;
        Notch.MouseLeave += Notch_MouseLeave;
    }

    protected override void OnClosed(EventArgs e)
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        base.OnClosed(e);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e) { }
    private void PrevButton_Click(object sender, RoutedEventArgs e) { }
    private void NextButton_Click(object sender, RoutedEventArgs e) { }
}