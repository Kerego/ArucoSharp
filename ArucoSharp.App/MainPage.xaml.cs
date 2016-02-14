using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VideoEffectComponent;
using VideoEffectLibrary;
using Windows.Devices.Enumeration;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ArucoSharp.App
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{ 
		// Receive notifications about rotation of the UI and apply any necessary rotation to the preview stream
		private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
		private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

		// Rotation metadata to apply to the preview stream (MF_MT_VIDEO_ROTATION)
		// Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
		private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

		// Prevent the screen from sleeping while the camera is running
		private readonly DisplayRequest _displayRequest = new DisplayRequest();

		// For listening to media property changes
		private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		// MediaCapture and its state variables
		private MediaCapture _mediaCapture;
		private bool _isInitialized = false;
		private bool _isPreviewing = false;

		// Information about the camera device
		private bool _mirroringPreview = false;
		private bool _externalCamera = false;

		PropertySet propertySet = new PropertySet();

		public MainPage()
        {
            this.InitializeComponent();
			this.Loaded += PageLoaded;
        }

		private async void PageLoaded(object sender, RoutedEventArgs e)
		{
			_displayOrientation = _displayInformation.CurrentOrientation;
			await InitializeCameraAsync();
			StorageFile sf = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///wordlist.txt", UriKind.Absolute));
			var _words = await FileIO.ReadLinesAsync(sf);
			propertySet["tolerance"] = 0.78;
			propertySet["hue"] = 320;
			propertySet["dictionary"] = _words;
			await _mediaCapture.AddVideoEffectAsync(new VideoEffectDefinition(typeof(TresholdVideoEffect).FullName, propertySet), MediaStreamType.VideoPreview);
			var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(400) };
			var textblockes = new List<TextBlock>();
			timer.Tick += (s, agrs) =>
			{
				List<Point> centers = new List<Point>();
				if (propertySet.ContainsKey("result"))
					Result.Text = propertySet["result"].ToString();
				if (propertySet.ContainsKey("centers"))
					centers = propertySet["centers"] as List<Point>;
				centers = centers.ToList();
				if (!centers.Any())
					return;
				if (textblockes.Count == centers.Count)
				{
					for (int i = 0; i < centers.Count; i++)
					{
						textblockes[i].Margin = new Thickness(0, centers[i].Y, centers[i].X, 0);
					}
				}
				else
				{
					foreach(TextBlock text in textblockes)
					{
						canvas.Children.Remove(text);
					}

					textblockes.Clear();

					for (int i = 0; i < centers.Count; i++)
					{
						var textblock = new TextBlock
						{
							Text = i.ToString(),
							Foreground = new SolidColorBrush(Colors.White),
							FontSize = 24,
							Margin = new Thickness(0, centers[i].Y, centers[i].X, 0)
						};
						canvas.Children.Add(textblock);
						textblockes.Add(textblock);
					}
				}


			};
			timer.Start();
		}

		private async Task StartPreviewAsync()
		{
			Debug.WriteLine("StartPreviewAsync");

			// Prevent the device from sleeping while the preview is running
			_displayRequest.RequestActive();

			// Register to listen for media property changes
			_systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;

			// Set the preview source in the UI and mirror it if necessary
			PreviewControl.Source = _mediaCapture;
			PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

			// Start the preview
			await _mediaCapture.StartPreviewAsync();
			_isPreviewing = true;

			// Initialize the preview to the current orientation
			if (_isPreviewing)
			{
				await SetPreviewRotationAsync();
			}
		}

		private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
		{
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				// Only handle this event if this page is currently being displayed
				if (args.Property == SystemMediaTransportControlsProperty.SoundLevel && Frame.CurrentSourcePageType == typeof(MainPage))
				{
					// Check to see if the app is being muted. If so, it is being minimized.
					// Otherwise if it is not initialized, it is being brought into focus.
					if (sender.SoundLevel == SoundLevel.Muted)
					{
						await CleanupCameraAsync();
					}
					else if (!_isInitialized)
					{
						await InitializeCameraAsync();
					}
				}
			});
		}

		private async Task InitializeCameraAsync()
		{
			Debug.WriteLine("InitializeCameraAsync");

			if (_mediaCapture == null)
			{
				// Attempt to get the back camera if one is available, but use any camera device if not
				var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

				if (cameraDevice == null)
				{
					Debug.WriteLine("No camera device found!");
					return;
				}

				// Create MediaCapture and its settings
				_mediaCapture = new MediaCapture();

				// Register for a notification when something goes wrong
				_mediaCapture.Failed += MediaCapture_Failed;

				var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

				// Initialize MediaCapture
				try
				{
					await _mediaCapture.InitializeAsync(settings);
					_isInitialized = true;
				}
				catch (UnauthorizedAccessException)
				{
					Debug.WriteLine("The app was denied access to the camera");
				}

				// If initialization succeeded, start the preview
				if (_isInitialized)
				{
					// Figure out where the camera is located
					if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
					{
						// No information on the location of the camera, assume it's an external camera, not integrated on the device
						_externalCamera = true;
					}
					else
					{
						// Camera is fixed on the device
						_externalCamera = false;

						// Only mirror the preview if the camera is on the front panel
						_mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
					}

					await StartPreviewAsync();
				}
			}
		}

		private async Task SetPreviewRotationAsync()
		{
			// Only need to update the orientation if the camera is mounted on the device
			if (_externalCamera) return;

			// Calculate which way and how far to rotate the preview
			int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

			// The rotation direction needs to be inverted if the preview is being mirrored
			if (_mirroringPreview)
			{
				rotationDegrees = (360 - rotationDegrees) % 360;
			}

			var t = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
			uint min = 2000;
			VideoEncodingProperties minResolution = null;
			for (int i = 0; i < t.Count; i++)
			{
				var prop = (VideoEncodingProperties)t[i];
				if(prop.Width < min && prop.Width > 600)
				{
					minResolution = prop;
					min = prop.Width;
				}
			}

			// Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
			var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
			props.Properties.Add(RotationKey, rotationDegrees);
			await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
			await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, minResolution);
		}


		private async Task StopPreviewAsync()
		{
			_isPreviewing = false;
			await _mediaCapture.StopPreviewAsync();

			// Use the dispatcher because this method is sometimes called from non-UI threads
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				PreviewControl.Source = null;

				// Allow the device to sleep now that the preview is stopped
				_displayRequest.RequestRelease();
			});
		}

		private async Task CleanupCameraAsync()
		{
			if (_isInitialized)
			{
				if (_isPreviewing)
				{
					// The call to stop the preview is included here for completeness, but can be
					// safely removed if a call to MediaCapture.Dispose() is being made later,
					// as the preview will be automatically stopped at that point
					await StopPreviewAsync();
				}

				_isInitialized = false;
			}

			if (_mediaCapture != null)
			{
				_mediaCapture.Failed -= MediaCapture_Failed;
				_mediaCapture.Dispose();
				_mediaCapture = null;
			}
		}

		private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
		{
			Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

			await CleanupCameraAsync();
		}

		private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
		{
			switch (orientation)
			{
				case DisplayOrientations.Portrait:
					return 90;
				case DisplayOrientations.LandscapeFlipped:
					return 180;
				case DisplayOrientations.PortraitFlipped:
					return 270;
				case DisplayOrientations.Landscape:
				default:
					return 0;
			}
		}

		private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
		{
			// Get available devices for capturing pictures
			var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

			// Get the desired camera by panel
			DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

			// If there is no device mounted on the desired panel, return the first device found
			return desiredDevice ?? allVideoDevices.FirstOrDefault();
		}

		private void ToleranceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			propertySet["tolerance"] = ToleranceSlider.Value;
			if(HueSlider != null)
				propertySet["hue"] = (int)HueSlider.Value;
		}
	}
}
