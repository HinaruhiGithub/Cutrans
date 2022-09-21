using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Input;
using Application = System.Windows.Application;
using Point = System.Drawing.Point;

using OpenCvSharp;
using OpenCvSharp.Extensions;
using Tesseract;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;

namespace Cutra
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        string langPath = Directory.GetCurrentDirectory() + @"\tessdata";
        //今回は英語のみ考える
        string langStr = "eng";

        string nowStack = "";

        const double thresholdNum = 80;

        System.Windows.Media.Color loadingColor = System.Windows.Media.Color.FromArgb(30, 241, 78, 82);
        System.Windows.Media.Color normalColor = System.Windows.Media.Color.FromArgb(30, 57, 112, 122);



        DebugImage debugWindow = new DebugImage();


        public MainWindow()
        {
            InitializeComponent();

            Debug.WriteLine(langPath);

#if DEBUG
            debugWindow.Show();
#endif
            ShowSpace.MouseLeftButtonDown += Window_MouseLeftButtonDown;
            ShowSpace.MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }


        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;

            this.DragMove();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UpdateOCR();
        }

        /// <summary>
        /// ウィンドウから画像を抽出、文字認識をスタートさせる。
        /// </summary>
        private async void UpdateOCR()
        {
            var leftTop = new Point((int)this.Left, (int)this.Top);
            var size = new Point((int)this.Width, (int)this.Height);

            CopyItem.IsEnabled = false;
            nowStack = "";

            // OpenCV
            var mat = await Task<List<(Mat, OpenCvSharp.Rect)>>.Run(() =>
            {
                var bitmap = CaptureScreen(leftTop, size);
                var bitmapSource = BitmapToBitmapSource(bitmap);
#if DEBUG
                bitmapSource.Freeze();
                debugWindow.ScreenShot.Dispatcher.BeginInvoke(new Action(() =>
                {
                    debugWindow.ScreenShot.Source = bitmapSource;
                }));
#endif
                var thresholdMats = TransformBitmapToMat(bitmap);

                return thresholdMats;
            });

            var brush = new SolidColorBrush();
            brush.Color = loadingColor;
            ShowSpace.Fill = brush;
            var isTranslateMode = EnableTranslate.IsChecked;

            var strings = await Task<List<string>>.Run(() =>
            {
                var detectedString = DetectString(mat, isTranslateMode);
                return detectedString;
            });

            foreach(var str in strings)
            {
                Debug.WriteLine(str);
                Debug.WriteLine("----------");
                nowStack += str + "/n";
            }

            brush.Color = normalColor;
            ShowSpace.Fill = brush;
            CopyItem.IsEnabled = true;
        }


        private BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            // Bitmapのハンドルを取得し、
            var hBitmap = bitmap.GetHbitmap();

            // CreateBitmapSourceFromHBitmap()で System.Windows.Media.Imaging.BitmapSource に変換する
            System.Windows.Media.Imaging.BitmapSource bitmapsource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());

            return bitmapsource;
        }

        /// <summary>
        /// OpenCVでbitmapを変形する
        /// </summary>
        /// <param name="bitmap"></param>
        private List<(Mat, OpenCvSharp.Rect)> TransformBitmapToMat(Bitmap bitmap)
        {
            var _srcMat = BitmapConverter.ToMat(bitmap);
            var grayMat = _srcMat.CvtColor(ColorConversionCodes.BGR2GRAY);
            var _thresholdMat = grayMat.Threshold(thresholdNum, 255, ThresholdTypes.Binary);

            //2値化
            var whiteAreaRatio = (float)_thresholdMat.CountNonZero() / (float)(_thresholdMat.Size().Width * _thresholdMat.Size().Height);
            Debug.WriteLine(whiteAreaRatio);
            //黒の方が大きかったら黒のほうにする。
            if (whiteAreaRatio < 0.5f)
            {
                _thresholdMat = grayMat.Threshold(thresholdNum, 255, ThresholdTypes.BinaryInv);
            }

            var binariedBitmap = BitmapToBitmapSource(BitmapConverter.ToBitmap(_thresholdMat));
#if DEBUG
            binariedBitmap.Freeze();
            debugWindow.Binaried.Dispatcher.BeginInvoke(new Action(() =>
            {
                debugWindow.Binaried.Source = binariedBitmap;
            }));
#endif


            //ジャグ配列
            OpenCvSharp.Point[][] contours;
            OpenCvSharp.HierarchyIndex[] hierarchyIndexes;
            //輪郭摘出
            _thresholdMat.FindContours(out contours, out hierarchyIndexes, RetrievalModes.External, ContourApproximationModes.ApproxNone);
            var _rectMats = new List<(Mat, OpenCvSharp.Rect)>();
            Debug.WriteLine($"Contours : {contours.Length}");
            foreach(var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                if(area <= 100000)
                {
                    continue;
                }
                else
                {
/*
                    if(area <= 200000)
                    {
                        //指定した面積の外接矩形の情報(座標値や幅、高さ)を得る
                        var retval = Cv2.BoundingRect(contour);
                        var rect = new OpenCvSharp.Rect(retval.TopLeft, new OpenCvSharp.Size(retval.Width, retval.Height));

                        //検出した外接矩形を切り出し、Listに追加する
                        _rectMats.Add(_thresholdMat.Clone(rect));
                    }
*/
                    //指定した面積の外接矩形の情報(座標値や幅、高さ)を得る
                    var retval = Cv2.BoundingRect(contour);
                    var rect = new OpenCvSharp.Rect(retval.TopLeft, new OpenCvSharp.Size(retval.Width, retval.Height));

                    //検出した外接矩形を切り出し、Listに追加する
                    _rectMats.Add((_thresholdMat.Clone(rect), rect));

                }
            }


            return _rectMats;
        }


        private List<string> DetectString(List<(Mat, OpenCvSharp.Rect)> _rectMats, bool isChecked)
        {
            List<string> readTexts = new List<string>();

            //検出した画像分繰り返す
            foreach (var rectMat in _rectMats)
            {
                using (var tesseract = new TesseractEngine(langPath, langStr))
                {
                    //使用する文字を指定する(今回は数字と.のみを検出)
                    //                    tesseract.SetVariable("tessedit_char_whitelist", "1234567890.");

                    //画像をMatからPixに変換
                    var rectPix = Pix.LoadFromMemory(rectMat.Item1.ToBytes());
                    
                    //画像データを渡してOcrを実行
                    Page page = tesseract.Process(rectPix);
                    Debug.WriteLine(page.GetText());
                    readTexts.Add(page.GetText());
                    //もし、翻訳をOnにしているなら、表示処理を行う。
                    if (isChecked)
                    {
                        var rect = rectMat.Item2;
                        ParentGrid.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var textBlock = new System.Windows.Controls.TextBlock();
//                            textBlock.Text = page.GetText();
                            textBlock.Text = "あああああああああああああああ";
                            textBlock.FontSize = rect.Height;
                            Debug.WriteLine(rect.Height);
                            textBlock.Margin = new Thickness(rect.Left, rect.Top, rect.Right, rect.Bottom);
                            Debug.WriteLine(rect);
                            ParentGrid.Children.Add(textBlock);
                        }));
                        //                        textBlock.Parent = ParentGrid;
/*
                        textBlock.Text = page.GetText();
                        textBlock.FontSize = rect.Height;
*/
                    }

                }
            }

            return readTexts;
        }

/*
        Pix mat8ToPix(Mat mat8)
        {
            Pix pixd = Pix.Create(mat8.Size().Width, mat8.Size().Height, 8);
            for (int y = 0; y < mat8.Rows; y++)
            {
                for (int x = 0; x < mat8.Cols; x++)
                {
                    pixd.
                    pixSetPixel(pixd, x, y, (l_uint32)mat8.At<uchar>(y, x));
                }
            }

            return pixd;
        }
*/


        //スクリーンショット撮影　
        private Bitmap CaptureScreen(Point leftTop, Point size)
        {
            //ウィンドウサイズの大きさの画像を生成
            Bitmap CaptureBitmap = new Bitmap(size.X, size.Y);
            Graphics ScreenGraphics = Graphics.FromImage(CaptureBitmap);

            //画像が...
            ScreenGraphics.CopyFromScreen(leftTop, new Point(0, 0), CaptureBitmap.Size);
            ScreenGraphics.Dispose();

            var bitmapsource = BitmapToBitmapSource(CaptureBitmap);
            Debug.WriteLine("うぇい");
            return CaptureBitmap;
        }


        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Copy_String(object sender, RoutedEventArgs e)
        {
            if (nowStack == "") return;
            System.Windows.Clipboard.SetText(nowStack);
        }
    }
}
