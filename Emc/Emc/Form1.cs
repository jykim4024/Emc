using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using OpenCvSharp;
using OpenCvSharp.Extensions;
using Tesseract;

namespace Emc
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            Mat src = Cv2.ImRead("card.png");
            //Mat src = Cv2.ImRead("card.jpg");

            OpenCvSharp.Point[] squares = Square(src);
            Mat square = DrawSquare(src, squares);
            Mat dst = PerspectiveTransform(src, squares);
            String texts = OCR(dst,"eng");

            textBox1.Multiline = true;
            int LineCount = textBox1.Lines.Length;

            string[] section = texts.Split(
                Environment.NewLine.ToCharArray(),
                StringSplitOptions.RemoveEmptyEntries
            );

            // 개행
            for (int i=0;i<section.Length;i++)
            {
                textBox1.Text += section[i] + Environment.NewLine;
            }

            Bitmap bfImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(src);
            pictureBox2.Image = bfImage;
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            Bitmap image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(dst);
            pictureBox1.Image = image;
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            // OCR result
            //textBox1.Text = texts;

            //Console.WriteLine(texts);
            //Cv2.ImShow("dst", dst);
            //Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        // 각도계산
        static double Angle(OpenCvSharp.Point pt1, OpenCvSharp.Point pt0, OpenCvSharp.Point pt2)
        {
            double u1 = pt1.X - pt0.X;
            double u2 = pt1.Y - pt0.Y;
            double v1 = pt2.X - pt0.X;
            double v2 = pt2.Y - pt0.Y;

            return (u1 * v1 + u2 * v2) / (Math.Sqrt(u1 * u1 + u2 * u2) * Math.Sqrt(v1 * v1 + v2 * v2));
        }

        // 사각형 검출 메서드
        public static OpenCvSharp.Point[] Square(Mat src)
        {
            Mat[] split = Cv2.Split(src);
            Mat blur = new Mat();
            Mat binary = new Mat();
            OpenCvSharp.Point[] squares = new OpenCvSharp.Point[4];

            int N = 10;
            double max = src.Size().Width * src.Size().Height * 0.9;
            double min = src.Size().Width * src.Size().Height * 0.1;

            // 임계값의 이진화 이미지 생성
            for (int channel = 0; channel < 3; channel++)
            {
                Cv2.GaussianBlur(split[channel], blur, new OpenCvSharp.Size(5, 5), 1);
                for (int i = 0; i < N; i++)
                {
                    Cv2.Threshold(blur, binary, i * 255 / N, 255, ThresholdTypes.Binary);

                    // 다각형 근사
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(binary, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxTC89KCOS);

                    Mat test = src.Clone();
                    Cv2.DrawContours(test, contours, -1, new Scalar(0, 0, 255), 3);

                    for (int j = 0; j < contours.Length; j++)
                    {
                        double perimeter = Cv2.ArcLength(contours[j], true);
                        OpenCvSharp.Point[] result = Cv2.ApproxPolyDP(contours[j], perimeter * 0.02, true);

                        double area = Cv2.ContourArea(result);
                        bool convex = Cv2.IsContourConvex(result);

                        if (result.Length == 4 && area > min && area < max && convex)
                        {
                            double cos = 0;
                            for (int k = 1; k < 5; k++)
                            {
                                double t = Math.Abs(Angle(result[(k - 1) % 4], result[k % 4], result[(k + 1) % 4]));
                                cos = cos > t ? cos : t;
                            }
                            if (cos < 0.15) squares = result;
                        }
                    }
                }
            }
            return squares;
        }

        // 검출된 사각형의 좌표로 이미지위에 사각형을 그린다
        public static Mat DrawSquare(Mat src, OpenCvSharp.Point[] squares)
        {
            Mat drawsquare = src.Clone();
            OpenCvSharp.Point[][] pts = new OpenCvSharp.Point[][] { squares };
            Cv2.Polylines(drawsquare, pts, true, Scalar.Yellow, 3, LineTypes.AntiAlias, 0);
            return drawsquare;
        }

        // 이미지 변환
        public static Mat PerspectiveTransform(Mat src, OpenCvSharp.Point[] squares)
        {
            Mat dst = new Mat();
            Moments moments = Cv2.Moments(squares);
            double cX = moments.M10 / moments.M00;
            double cY = moments.M01 / moments.M00;

            Point2f[] src_pts = new Point2f[4];
            for (int i = 0; i < squares.Length; i++)
            {
                if (cX > squares[i].X && cY > squares[i].Y) src_pts[0] = squares[i];
                if (cX > squares[i].X && cY < squares[i].Y) src_pts[1] = squares[i];
                if (cX < squares[i].X && cY > squares[i].Y) src_pts[2] = squares[i];
                if (cX < squares[i].X && cY < squares[i].Y) src_pts[3] = squares[i];
            }

            Point2f[] dst_pts = new Point2f[4]
            {
                new Point2f(0, 0),
                new Point2f(0, src.Height),
                new Point2f(src.Width, 0),
                new Point2f(src.Width, src.Height)
            };

            Mat matrix = Cv2.GetPerspectiveTransform(src_pts, dst_pts);
            Cv2.WarpPerspective(src, dst, matrix, new OpenCvSharp.Size(src.Width, src.Height));
            return dst;
        }

        public static string OCR(Mat src, string langCd)
        {
            Bitmap bitmap = src.ToBitmap();
            TesseractEngine ocr = new TesseractEngine("../../tessdata", langCd, EngineMode.LstmOnly);
            Page texts = ocr.Process(bitmap);
            string sentence = texts.GetText().Trim();

            /*
            string[] section = sentence.Split(
                Environment.NewLine.ToCharArray(),
                StringSplitOptions.RemoveEmptyEntries
            );

            foreach (var paragraph in section)
            {
                this.textBox1.Text = texts;
            }
            */

            return sentence;
        }

    }
}
