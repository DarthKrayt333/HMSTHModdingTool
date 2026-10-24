using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Microsoft.Win32;

namespace HMSTHPreview
{
    public partial class MainWindow : Window
    {
        private ModelPreviewData _currentModel;
        private GDTBArchive _gdtbArchive;
        private string _gdtbPath;
        private bool _dragging;
        private Point _lastMousePoint;
        private double _yaw;
        private double _pitch;
        private double _cameraDistance = 6.0;
        private double _defaultCameraDistance = 6.0;
        private Point3D _cameraTarget = new Point3D(0, 0, 0);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1]))
            {
                string extension = Path.GetExtension(args[1]).ToLowerInvariant();
                try
                {
                    if (extension == ".obj" || extension == ".glb") LoadModel(args[1]);
                    else if (extension == ".gdtb") LoadGdtb(args[1]);
                    else LoadTexture(args[1]);
                }
                catch (Exception ex)
                {
                    ShowError("Could not open startup file", ex);
                }
            }
        }

        private void OpenModel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a model",
                Filter = "3D model files (*.obj;*.glb)|*.obj;*.glb|Wavefront OBJ (*.obj)|*.obj|glTF Binary (*.glb)|*.glb|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                try { LoadModel(dialog.FileName); }
                catch (Exception ex) { ShowError("Model preview failed", ex); }
            }
        }

        private void OpenTexture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a texture or image",
                Filter = "Image files|*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.tif;*.tiff;*.wdp;*.ico|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                try { LoadTexture(dialog.FileName); }
                catch (Exception ex) { ShowError("Image preview failed", ex); }
            }
        }

        private void OpenGdtb_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a GDTB texture archive",
                Filter = "GDTB archives (*.gdtb)|*.gdtb|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                try { LoadGdtb(dialog.FileName); }
                catch (Exception ex) { ShowError("GDTB preview failed", ex); }
            }
        }

        private void LoadModel(string path)
        {
            ModelPreviewData loaded = ModelPreviewLoader.Load(path);
            _currentModel = loaded;
            SceneVisual.Content = loaded.Model;
            ModelPlaceholder.Visibility = Visibility.Collapsed;
            _cameraTarget = loaded.Center;
            _defaultCameraDistance = Math.Max(loaded.Radius * 3.2, 0.1);
            _cameraDistance = _defaultCameraDistance;
            _yaw = 0;
            _pitch = 0.18;
            UpdateCamera();
            PreviewTabs.SelectedIndex = 0;

            string message = Path.GetFileName(path) + " — " + loaded.VertexCount + " vertices, " +
                loaded.TriangleCount + " triangles, " + loaded.MaterialCount + " mesh/material group(s).";
            if (loaded.Warnings.Count > 0)
                message += " Preview warnings: " + loaded.Warnings.Count + ". See details below.";
            StatusText.Text = message;
            if (loaded.Warnings.Count > 0)
                MessageBox.Show(this, string.Join(Environment.NewLine, loaded.Warnings.ToArray()),
                    "Model preview warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void LoadTexture(string path)
        {
            BitmapSource image = ModelPreviewLoader.LoadImageFromFile(path);
            TextureImage.Source = image;
            ImagePlaceholder.Visibility = Visibility.Collapsed;
            PreviewTabs.SelectedIndex = 1;
            StatusText.Text = Path.GetFileName(path) + " — " + image.PixelWidth + " × " + image.PixelHeight + " pixels.";
        }

        private void LoadGdtb(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0 || fileInfo.Length > 512L * 1024L * 1024L)
                throw new InvalidDataException("GDTB input must be between 1 byte and 512 MiB for preview.");

            var archive = new GDTBArchive(path);
            archive.Load();
            _gdtbArchive = archive;
            _gdtbPath = Path.GetFullPath(path);
            GdtbTextureList.Items.Clear();
            GdtbTextureImage.Source = null;
            GdtbTextureInfo.Text = string.Empty;
            GdtbPlaceholder.Visibility = Visibility.Visible;
            for (int i = 0; i < archive.TextureCount; i++)
            {
                GDTBTexture texture = archive.GetTextureInfo(i);
                GdtbTextureList.Items.Add(texture.ToString());
            }
            GdtbSourceText.Text = Path.GetFileName(_gdtbPath) + Environment.NewLine +
                archive.TextureCount + " texture(s) loaded. Select an entry to decode its BMP preview.";
            PreviewTabs.SelectedIndex = 2;
            StatusText.Text = "Loaded GDTB: " + Path.GetFileName(path) + " — " + archive.TextureCount + " texture(s).";
            if (archive.TextureCount > 0) GdtbTextureList.SelectedIndex = 0;
        }

        private void GdtbTextureList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_gdtbArchive == null || GdtbTextureList.SelectedIndex < 0) return;
            try
            {
                int index = GdtbTextureList.SelectedIndex;
                GDTBTexture texture = _gdtbArchive.GetTextureInfo(index);
                byte[] bmpBytes = _gdtbArchive.GetTextureBitmap(index);
                BitmapSource bitmap = ModelPreviewLoader.LoadImageFromBytes(bmpBytes);
                GdtbTextureImage.Source = bitmap;
                GdtbPlaceholder.Visibility = Visibility.Collapsed;
                GdtbTextureInfo.Text = texture.ToString() + " · Offset 0x" + texture.BodyOffset.ToString("X") +
                    " · Image bytes " + texture.ImageSize;
                StatusText.Text = "GDTB texture " + index + " previewed from " + Path.GetFileName(_gdtbPath) + ".";
            }
            catch (Exception ex)
            {
                GdtbTextureImage.Source = null;
                GdtbTextureInfo.Text = "Texture decode failed: " + ex.Message;
                StatusText.Text = "GDTB texture preview failed.";
            }
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModel == null) return;
            _cameraTarget = _currentModel.Center;
            _cameraDistance = _defaultCameraDistance;
            _yaw = 0;
            _pitch = 0.18;
            UpdateCamera();
        }

        private void ModelViewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentModel == null || e.ChangedButton != MouseButton.Left) return;
            _dragging = true;
            _lastMousePoint = e.GetPosition(ModelViewport);
            ModelViewport.CaptureMouse();
        }

        private void ModelViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(ModelViewport);
            double deltaX = current.X - _lastMousePoint.X;
            double deltaY = current.Y - _lastMousePoint.Y;
            _lastMousePoint = current;
            _yaw += deltaX * 0.008;
            _pitch = Math.Max(-1.45, Math.Min(1.45, _pitch + deltaY * 0.008));
            UpdateCamera();
        }

        private void ModelViewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _dragging = false;
            ModelViewport.ReleaseMouseCapture();
        }

        private void ModelViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentModel == null) return;
            _cameraDistance *= e.Delta > 0 ? 0.85 : 1.18;
            double minimum = Math.Max(_currentModel.Radius * 0.05, 0.001);
            double maximum = Math.Max(_currentModel.Radius * 100.0, 100.0);
            _cameraDistance = Math.Max(minimum, Math.Min(maximum, _cameraDistance));
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            double cosPitch = Math.Cos(_pitch);
            Vector3D offset = new Vector3D(
                _cameraDistance * cosPitch * Math.Sin(_yaw),
                _cameraDistance * Math.Sin(_pitch),
                _cameraDistance * cosPitch * Math.Cos(_yaw));
            PreviewCamera.Position = _cameraTarget + offset;
            PreviewCamera.LookDirection = _cameraTarget - PreviewCamera.Position;
            PreviewCamera.UpDirection = new Vector3D(0, 1, 0);
            PreviewCamera.NearPlaneDistance = Math.Max(0.0001, _cameraDistance / 10000.0);
            PreviewCamera.FarPlaneDistance = Math.Max(1000.0, _cameraDistance * 100.0);
        }

        private void ShowError(string title, Exception ex)
        {
            StatusText.Text = title + ": " + ex.Message;
            MessageBox.Show(this, ex.GetType().Name + ": " + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
