using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VSP.Core.Logging;
using VSP.Device.Drivers.ONVIF;
using VSP.Domain.Enums;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Player;

[Collection("AppLog")]
public class LiveViewViewModelTests
{
    [Fact]
    public void LoadCamera_WithRtspUrl_StartsController()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller);

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.True(viewModel.HasCamera);
        Assert.Equal(camera, viewModel.Camera);
        Assert.True(controller.StartCalled);
    }

    [Fact]
    public void LoadCamera_WithoutRtspUrl_DoesNotStartControllerAndReportsStatus()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller);

        var camera = new EntityCamera { Name = "Unconfigured", RtspUrl = "", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.True(viewModel.HasCamera);
        Assert.False(controller.StartCalled);
        Assert.Contains("no stream URL configured", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadCamera_UsesConfiguredRtspPort_WhenUrlHasNoPort()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (url, _, _) =>
        {
            capturedUrl = url;
            return controller;
        });

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", RtspPort = 8554, ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://192.168.1.10:8554/stream", capturedUrl);
    }

    [Fact]
    public void LoadCamera_PrefersExplicitUrlPort_OverConfiguredRtspPort()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (url, _, _) =>
        {
            capturedUrl = url;
            return controller;
        });

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10:554/stream", RtspPort = 8554, ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://192.168.1.10:554/stream", capturedUrl);
    }

    [Fact]
    public void LoadCamera_OnvifCameraWithBlankRtspUrl_ResolvesStreamUriAndStartsController()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var resolver = new FakeOnvifStreamUriResolver(OnvifStreamUriResolution.Success("rtsp://192.168.0.89:1025/Streaming/Channels/101"));
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (url, _, _) =>
            {
                capturedUrl = url;
                return controller;
            },
            onvifStreamUriResolver: resolver);

        var camera = new EntityCamera { Name = "Onvif Cam", RtspUrl = "", ConnectionType = DeviceConnectionType.ONVIF, HttpPort = 1025 };
        viewModel.LoadCamera(camera);

        Assert.True(controller.StartCalled);
        Assert.Equal("rtsp://192.168.0.89:1025/Streaming/Channels/101", capturedUrl);
        Assert.DoesNotContain("no stream URL configured", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadCamera_OnvifCameraWithCredentials_AttachesCredentialsToResolvedStreamUri()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var resolver = new FakeOnvifStreamUriResolver(OnvifStreamUriResolution.Success("rtsp://192.168.0.89:1025/Streaming/Channels/101"));
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (url, _, _) =>
            {
                capturedUrl = url;
                return controller;
            },
            onvifStreamUriResolver: resolver,
            credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Onvif Cam", RtspUrl = "", ConnectionType = DeviceConnectionType.ONVIF, HttpPort = 1025, Username = "admin", Password = "secret" };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://admin:secret@192.168.0.89:1025/Streaming/Channels/101", capturedUrl);
    }

    [Fact]
    public void LoadCamera_RtspCameraWithCredentials_AttachesCredentialsToConfiguredUrl()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (url, _, _) =>
        {
            capturedUrl = url;
            return controller;
        }, credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP, Username = "admin", Password = "secret" };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://admin:secret@192.168.1.10:554/stream", capturedUrl);
    }

    [Fact]
    public void LoadCamera_OnvifCameraWithCredentials_DoesNotPersistAuthenticatedUriOntoCamera()
    {
        var controller = new FakeMediaController();
        var resolver = new FakeOnvifStreamUriResolver(OnvifStreamUriResolution.Success("rtsp://192.168.0.89:1025/Streaming/Channels/101"));
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (_, _, _) => controller,
            onvifStreamUriResolver: resolver,
            credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Onvif Cam", RtspUrl = "", ConnectionType = DeviceConnectionType.ONVIF, HttpPort = 1025, Username = "admin", Password = "secret" };
        viewModel.LoadCamera(camera);

        Assert.Equal("", camera.RtspUrl);
        Assert.Equal("secret", camera.Password);
    }

    [Fact]
    public void LoadCamera_RtspCameraWithCredentials_DoesNotPersistAuthenticatedUriOntoCamera()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (_, _, _) => controller,
            credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP, Username = "admin", Password = "secret" };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://192.168.1.10/stream", camera.RtspUrl);
    }

    [Fact]
    public void LoadCamera_RtspUrlAlreadyHasUserInfo_ConfiguredCredentialsReplaceIt_NoDuplication()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (url, _, _) =>
        {
            capturedUrl = url;
            return controller;
        }, credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://olduser:oldpass@192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP, Username = "admin", Password = "secret" };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://admin:secret@192.168.1.10:554/stream", capturedUrl);
    }

    [Fact]
    public void LoadCamera_RtspUrlAlreadyHasUserInfo_NoConfiguredCredentials_PreservesExistingUserInfo()
    {
        var controller = new FakeMediaController();
        string? capturedUrl = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (url, _, _) =>
        {
            capturedUrl = url;
            return controller;
        });

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://olduser:oldpass@192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.Equal("rtsp://olduser:oldpass@192.168.1.10:554/stream", capturedUrl);
    }

    [Fact]
    public void LoadCamera_WithCredentials_StatusMessageNeverContainsRawCredentials()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (_, _, _) => controller,
            credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP, Username = "admin", Password = "super-secret-value" };
        viewModel.LoadCamera(camera);

        Assert.DoesNotContain("super-secret-value", viewModel.StatusMessage);
    }

    // Task-AI00B Phase 7C: credential-boundary diagnostic coverage. Uses deliberately
    // recognizable fake secret strings and asserts they never appear in captured log output.
    [Fact]
    public void LoadCamera_OnvifCameraWithCredentials_LogsCredentialBoundaryDiagnosticsWithoutSecrets()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var controller = new FakeMediaController();
        var resolver = new FakeOnvifStreamUriResolver(OnvifStreamUriResolution.Success("rtsp://192.168.0.89:1025/Streaming/Channels/101"));
        var viewModel = new LiveViewViewModel(
            Dispatcher.CurrentDispatcher,
            (_, _, _) => controller,
            onvifStreamUriResolver: resolver,
            credentialProvider: _ => new VSP.Domain.Entities.CameraCredentials("admin", "secret"));

        var camera = new EntityCamera
        {
            Name = "Onvif Cam",
            RtspUrl = "",
            ConnectionType = DeviceConnectionType.ONVIF,
            HttpPort = 1025,
            Username = "PHASE7C-FAKE-USER",
            Password = "PHASE7C-FAKE-PASSWORD-99887766"
        };
        viewModel.LoadCamera(camera);

        var boundaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("Live View credential boundary:"));
        Assert.Equal(LogLevel.Info, boundaryCall.Level);
        Assert.Contains("cameraConnectionType=ONVIF", boundaryCall.Message);
        Assert.Contains("usernamePresent=True", boundaryCall.Message);
        Assert.Contains("passwordPresent=True", boundaryCall.Message);
        Assert.Contains("preUriHasUserInfo=False", boundaryCall.Message);
        Assert.Contains("runtimeUriHasUserInfo=True", boundaryCall.Message);
        Assert.Contains("runtimeUriSchemeMatchesPreUri=True", boundaryCall.Message);
        Assert.Contains("runtimeUriHostMatchesPreUri=True", boundaryCall.Message);
        Assert.Contains("runtimeUriPortMatchesPreUri=True", boundaryCall.Message);
        Assert.Contains("runtimeUriPathMatchesPreUri=True", boundaryCall.Message);
        Assert.Contains("runtimeUriQueryMatchesPreUri=True", boundaryCall.Message);

        AssertNoCallContains(recorder, "PHASE7C-FAKE-USER");
        AssertNoCallContains(recorder, "PHASE7C-FAKE-PASSWORD-99887766");
        AssertNoCallContains(recorder, "rtsp://PHASE7C-FAKE-USER:PHASE7C-FAKE-PASSWORD-99887766@192.168.0.89:1025/Streaming/Channels/101");
    }

    [Fact]
    public void LoadCamera_RtspUrlAlreadyHasUserInfo_LogsCredentialBoundaryDiagnosticsWithoutSecrets()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller);

        var camera = new EntityCamera
        {
            Name = "Front Door",
            RtspUrl = "rtsp://PHASE7C-OLD-USER:PHASE7C-OLD-PASS@192.168.1.10/stream",
            ConnectionType = DeviceConnectionType.RTSP,
            Username = "PHASE7C-FAKE-USER",
            Password = "PHASE7C-FAKE-PASSWORD-99887766"
        };
        viewModel.LoadCamera(camera);

        var boundaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("Live View credential boundary:"));
        Assert.Contains("cameraConnectionType=RTSP", boundaryCall.Message);
        Assert.Contains("preUriHasUserInfo=True", boundaryCall.Message);
        Assert.Contains("runtimeUriHasUserInfo=True", boundaryCall.Message);
        Assert.Contains("runtimeUriHostMatchesPreUri=True", boundaryCall.Message);
        Assert.Contains("runtimeUriPathMatchesPreUri=True", boundaryCall.Message);

        AssertNoCallContains(recorder, "PHASE7C-FAKE-USER");
        AssertNoCallContains(recorder, "PHASE7C-FAKE-PASSWORD-99887766");
        AssertNoCallContains(recorder, "PHASE7C-OLD-USER");
        AssertNoCallContains(recorder, "PHASE7C-OLD-PASS");
    }

    [Fact]
    public void LoadCamera_NoConfiguredUsername_LogsUsernamePresentFalseAndDoesNotChangeUserInfo()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller);

        var camera = new EntityCamera
        {
            Name = "Front Door",
            RtspUrl = "rtsp://192.168.1.10/stream",
            ConnectionType = DeviceConnectionType.RTSP
        };
        viewModel.LoadCamera(camera);

        var boundaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("Live View credential boundary:"));
        Assert.Contains("usernamePresent=False", boundaryCall.Message);
        Assert.Contains("passwordPresent=False", boundaryCall.Message);
        Assert.Contains("preUriHasUserInfo=False", boundaryCall.Message);
        Assert.Contains("runtimeUriHasUserInfo=False", boundaryCall.Message);
    }

    private static void AssertNoCallContains(RecordingLogger recorder, string forbiddenSubstring)
    {
        Assert.All(recorder.Calls, call => Assert.DoesNotContain(forbiddenSubstring, call.Message));
    }

    [Fact]
    public void LoadCamera_OnvifCameraWithBlankRtspUrl_DoesNotPersistResolvedStreamUriOntoCamera()
    {
        var controller = new FakeMediaController();
        var resolver = new FakeOnvifStreamUriResolver(OnvifStreamUriResolution.Success("rtsp://192.168.0.89:1025/Streaming/Channels/101"));
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller, onvifStreamUriResolver: resolver);

        var camera = new EntityCamera { Name = "Onvif Cam", RtspUrl = "", ConnectionType = DeviceConnectionType.ONVIF, HttpPort = 1025 };
        viewModel.LoadCamera(camera);

        Assert.Equal("", camera.RtspUrl);
    }

    [Fact]
    public void LoadCamera_OnvifResolutionFails_DoesNotStartControllerAndDoesNotFallBackToStoredRtspUrl()
    {
        var controller = new FakeMediaController();
        var resolver = new FakeOnvifStreamUriResolver(
            OnvifStreamUriResolution.Failure(OnvifStreamUriFailureReason.NoMediaProfiles, "The camera did not return any usable ONVIF media profiles."));
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller, onvifStreamUriResolver: resolver);

        // A stray/leftover manually-typed RtspUrl must never be used as a silent fallback --
        // an ONVIF failure must stay visible as an ONVIF failure.
        var camera = new EntityCamera { Name = "Onvif Cam", RtspUrl = "rtsp://stale-manual-entry/stream", ConnectionType = DeviceConnectionType.ONVIF, HttpPort = 1025 };
        viewModel.LoadCamera(camera);

        Assert.False(controller.StartCalled);
        Assert.Contains("ONVIF", viewModel.StatusMessage);
        Assert.DoesNotContain("stale-manual-entry", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadCamera_RtspCamera_NeverInvokesOnvifResolver()
    {
        var controller = new FakeMediaController();
        var resolver = new ThrowingOnvifStreamUriResolver();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, _) => controller, onvifStreamUriResolver: resolver);

        var camera = new EntityCamera { Name = "Rtsp Cam", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.True(controller.StartCalled);
    }

    [Fact]
    public void ControllerStateChanged_ToConnected_UpdatesStatusAndState()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.Equal(MediaControllerState.Connected, viewModel.State);
        Assert.Contains("Live:", viewModel.StatusMessage);
    }

    [Fact]
    public void ControllerStateChanged_ToError_IncludesErrorMessage()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(
            MediaControllerState.Error,
            new MediaError { Category = MediaErrorCategory.Connection, Message = "connection refused" });
        PumpDispatcher(dispatcher);

        Assert.Contains("connection refused", viewModel.StatusMessage);
    }

    [Fact]
    public void FrameRendered_RaisesPropertyChangedForCurrentFrameSource()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        // Connected fires before any frame has decoded (matches production: MediaController sets
        // Connected immediately after decoder construction), so CurrentFrameSource is still null
        // at this point -- exactly the state that produced Item F's black Live View before this
        // fix, since nothing re-pulled the property once a real frame later became available.
        Assert.Null(viewModel.CurrentFrameSource);

        var raisedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        var fakeRenderer = (FakeFrameRenderer)controller.Renderer;
        fakeRenderer.CurrentFrameSource = new BitmapImage();
        fakeRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Contains(nameof(LiveViewViewModel.CurrentFrameSource), raisedProperties);
        Assert.Same(fakeRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
    }

    [Fact]
    public void FrameRendered_AfterControllerDetached_SubscriptionIsRemoved()
    {
        var controller1 = new FakeMediaController();
        var controller2 = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var pendingControllers = new Queue<FakeMediaController>(new[] { controller1, controller2 });
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => pendingControllers.Dequeue());

        viewModel.LoadCamera(new EntityCamera { Name = "Camera 1", RtspUrl = "rtsp://host1/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller1.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        // Loading a second camera detaches controller1 -- must unsubscribe from
        // controller1.Renderer.FrameRendered as part of that detach.
        viewModel.LoadCamera(new EntityCamera { Name = "Camera 2", RtspUrl = "rtsp://host2/stream", ConnectionType = DeviceConnectionType.RTSP });
        Assert.True(controller1.DisposeCalled);

        var raisedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        // Simulates a frame callback from controller1 still in flight (e.g. already queued on
        // the dispatcher) at the moment it was detached.
        var detachedRenderer = (FakeFrameRenderer)controller1.Renderer;
        detachedRenderer.CurrentFrameSource = new BitmapImage();
        detachedRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.DoesNotContain(nameof(LiveViewViewModel.CurrentFrameSource), raisedProperties);
    }

    [Fact]
    public void FrameRendered_FromOldDetachedController_CannotUpdateCurrentViewModel()
    {
        var controller1 = new FakeMediaController();
        var controller2 = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var pendingControllers = new Queue<FakeMediaController>(new[] { controller1, controller2 });
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => pendingControllers.Dequeue());

        viewModel.LoadCamera(new EntityCamera { Name = "Camera 1", RtspUrl = "rtsp://host1/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller1.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        viewModel.LoadCamera(new EntityCamera { Name = "Camera 2", RtspUrl = "rtsp://host2/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller2.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        var currentRenderer = (FakeFrameRenderer)controller2.Renderer;
        currentRenderer.CurrentFrameSource = new BitmapImage();
        currentRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Same(currentRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);

        // A late frame from the now-detached controller1 must never leak into the ViewModel
        // that's now hosting controller2 -- neither by updating CurrentFrameSource nor by
        // spuriously notifying it.
        var staleRenderer = (FakeFrameRenderer)controller1.Renderer;
        staleRenderer.CurrentFrameSource = new BitmapImage();
        staleRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Same(currentRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
        Assert.NotSame(staleRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
    }

    [Fact]
    public void PauseCommand_OnlyExecutableWhenConnected()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        Assert.False(viewModel.PauseCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.PauseCommand.CanExecute(null));
    }

    // Item F (Stop/Restart control-state defect): Play/Start Live is only offered once there is
    // something to (re)start from -- Disconnected (after Stop) or Error -- never while Connecting,
    // Connected, Paused, or Reconnecting.
    [Fact]
    public void PlayCommand_OnlyExecutableWhenStoppedOrError()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        // Idle (Connecting hasn't been raised yet): not executable.
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connecting);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Paused);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Reconnecting);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Disconnected);
        PumpDispatcher(dispatcher);
        Assert.True(viewModel.PlayCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Error, new MediaError { Category = MediaErrorCategory.Connection, Message = "boom" });
        PumpDispatcher(dispatcher);
        Assert.True(viewModel.PlayCommand.CanExecute(null));
    }

    // Item F: Stop must never be clickable while there is nothing playing/paused to stop --
    // in particular not merely because a camera is loaded (the pre-fix `HasCamera` gate).
    [Fact]
    public void StopCommand_OnlyExecutableWhenPlayingOrPaused()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        Assert.False(viewModel.StopCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connecting);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.StopCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);
        Assert.True(viewModel.StopCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Paused);
        PumpDispatcher(dispatcher);
        Assert.True(viewModel.StopCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Disconnected);
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.StopCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Error, new MediaError { Category = MediaErrorCategory.Connection, Message = "boom" });
        PumpDispatcher(dispatcher);
        Assert.False(viewModel.StopCommand.CanExecute(null));
    }

    // Item F core regression: Stop -> Play must reconnect the same camera through the existing
    // LoadCamera path (fresh ONVIF resolve, fresh credential attach) with the old controller
    // disposed first and exactly one controller ever live -- never a duplicate session.
    [Fact]
    public void PlayCommand_AfterStop_ReconnectsToSameCameraWithoutDuplicateController()
    {
        var controller1 = new FakeMediaController();
        var controller2 = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var pendingControllers = new Queue<FakeMediaController>(new[] { controller1, controller2 });
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => pendingControllers.Dequeue());

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);
        controller1.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        viewModel.StopCommand.Execute(null);
        PumpDispatcher(dispatcher);
        Assert.True(controller1.StopCalled);

        // FakeMediaController.StopAsync doesn't itself drive a state transition (unlike the real
        // controller's state machine) -- simulate the state reaching Disconnected the way
        // production does once Stop completes.
        controller1.RaiseStateChanged(MediaControllerState.Disconnected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.PlayCommand.CanExecute(null));
        Assert.False(controller1.DisposeCalled);

        viewModel.PlayCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller1.DisposeCalled);
        Assert.True(controller2.StartCalled);
        Assert.Same(camera, viewModel.Camera);
        Assert.Empty(pendingControllers);
    }

    // Item F core regression: Error -> Play must go through the identical reconnect flow as
    // Stop -> Play (same LoadCamera path, old controller disposed first, no duplicate session).
    [Fact]
    public void PlayCommand_AfterError_ReconnectsToSameCameraWithoutDuplicateController()
    {
        var controller1 = new FakeMediaController();
        var controller2 = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var pendingControllers = new Queue<FakeMediaController>(new[] { controller1, controller2 });
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => pendingControllers.Dequeue());

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);
        controller1.RaiseStateChanged(MediaControllerState.Error, new MediaError { Category = MediaErrorCategory.Connection, Message = "connection refused" });
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.PlayCommand.CanExecute(null));
        Assert.False(controller1.DisposeCalled);

        viewModel.PlayCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller1.DisposeCalled);
        Assert.True(controller2.StartCalled);
        Assert.Same(camera, viewModel.Camera);
        Assert.Empty(pendingControllers);
    }

    // RC1-R04 (Recording/Playback Storage Contract): the controller factory must receive the
    // exact Id of the camera passed to LoadCamera, taken from the Camera instance already in
    // scope at the real call site -- not any other global/current-camera property -- so
    // MediaController can route this camera's recordings into its own per-camera directory.
    [Fact]
    public void LoadCamera_PassesExactSelectedCameraIdToControllerFactory()
    {
        var controller = new FakeMediaController();
        Guid? capturedCameraId = null;
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, cameraId) =>
        {
            capturedCameraId = cameraId;
            return controller;
        });

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.Equal(camera.Id, capturedCameraId);
    }

    // Proves the id is re-read per LoadCamera call (not cached/stale from a prior camera), which
    // is what actually guarantees Camera A's and Camera B's recordings end up in distinct
    // per-camera directories once MediaController/RecordingPathProvider act on this id.
    [Fact]
    public void LoadCamera_DifferentCameras_EachPassesItsOwnIdToControllerFactory()
    {
        var controllerA = new FakeMediaController();
        var controllerB = new FakeMediaController();
        var pendingControllers = new Queue<FakeMediaController>(new[] { controllerA, controllerB });
        var capturedCameraIds = new List<Guid>();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _, cameraId) =>
        {
            capturedCameraIds.Add(cameraId);
            return pendingControllers.Dequeue();
        });

        var cameraA = new EntityCamera { Name = "Camera A", RtspUrl = "rtsp://host-a/stream", ConnectionType = DeviceConnectionType.RTSP };
        var cameraB = new EntityCamera { Name = "Camera B", RtspUrl = "rtsp://host-b/stream", ConnectionType = DeviceConnectionType.RTSP };

        viewModel.LoadCamera(cameraA);
        viewModel.LoadCamera(cameraB);

        Assert.Equal([cameraA.Id, cameraB.Id], capturedCameraIds);
        Assert.NotEqual(cameraA.Id, cameraB.Id);
    }

    [Fact]
    public void StartRecordingCommand_OnlyExecutableWhenConnectedAndNotRecording()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.StartRecordingCommand.CanExecute(null));
        Assert.False(viewModel.StopRecordingCommand.CanExecute(null));

        // FakeMediaController.StartRecordingAsync/StopRecordingAsync complete synchronously
        // (Task.CompletedTask), so the queued RaiseAllChanged dispatcher callback is already
        // pending by the time Execute returns -- no real async handoff, so no need to leave
        // this thread (PumpDispatcher below must run on the same thread that created
        // `dispatcher`; hopping via Task.Delay here would move the continuation to an
        // arbitrary thread-pool thread and hang PushFrame on the wrong thread).
        viewModel.StartRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StartRecordingCalled);
        Assert.True(viewModel.IsRecording);
        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));
        Assert.True(viewModel.StopRecordingCommand.CanExecute(null));

        viewModel.StopRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StopRecordingCalled);
        Assert.False(viewModel.IsRecording);
    }

    [Fact]
    public void StopRecordingCommand_TriggersRetentionAfterRecordingStops()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var completedCameraIds = new List<Guid>();
        var viewModel = new LiveViewViewModel(
            dispatcher,
            (_, _, _) => controller,
            Role.Admin,
            recordingCompleted: completedCameraIds.Add);
        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP };

        viewModel.LoadCamera(camera);
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        viewModel.StartRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);
        viewModel.StopRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StopRecordingCalled);
        Assert.Equal([camera.Id], completedCameraIds);
        Assert.False(controller.IsRecording);
    }

    // Epic-018 Decision 1 (§8.1): Recording is Admin-only. Previously ungated -- a gap discovered
    // during Milestone 18D's manual-validation preparation, fixed under the same approved
    // decision, not a new feature.
    [Fact]
    public void OperatorRole_StartAndStopRecordingCommands_ReportCannotExecuteEvenWhenConnected()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller, Role.Operator);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));

        // Defense in depth: even if Start were forced (RelayCommand.Execute does not itself
        // consult CanExecute), IsRecording would still be false, so Stop stays blocked too.
        Assert.False(viewModel.StopRecordingCommand.CanExecute(null));
    }

    [Fact]
    public void AdminRole_StartRecordingCommand_UnaffectedRegressionGuard()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _, _) => controller, Role.Admin);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.StartRecordingCommand.CanExecute(null));
    }

    private static void PumpDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    internal sealed class FakeMediaController : IMediaController
    {
        public MediaControllerState State { get; private set; } = MediaControllerState.Idle;

        public MediaSessionStatistics Statistics { get; } = MediaSessionStatistics.Empty;

        public IFrameRenderer Renderer { get; } = new FakeFrameRenderer();

        public IDispatcherMetrics DispatcherMetrics { get; } = new FakeDispatcherMetrics();

        public IMediaClock Clock { get; } = new VSP.Player.Pipeline.MediaClock();

        public TimeSpan? Duration => null;

        public bool IsRecording { get; private set; }

        public bool StartCalled { get; private set; }

        public bool StopCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public bool StartRecordingCalled { get; private set; }

        public bool StopRecordingCalled { get; private set; }

        public event EventHandler<MediaControllerStateChangedEventArgs>? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StartRecordingAsync()
        {
            StartRecordingCalled = true;
            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task StopRecordingAsync()
        {
            StopRecordingCalled = true;
            IsRecording = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }

        public void RaiseStateChanged(MediaControllerState state, MediaError? error = null)
        {
            State = state;
            StateChanged?.Invoke(this, new MediaControllerStateChangedEventArgs { State = state, Error = error });
        }
    }

    internal sealed class FakeFrameRenderer : IFrameRenderer
    {
        public ImageSource? CurrentFrameSource { get; set; }

        public bool IsActive { get; private set; }

        public event EventHandler? FrameRendered;

        public void OnFrame(DecodedFrame frame)
        {
        }

        public void Start() => IsActive = true;

        public void Stop() => IsActive = false;

        public void Dispose()
        {
        }

        /// <summary>Test seam: simulates WpfFrameRenderer raising FrameRendered after a real frame paints.</summary>
        public void RaiseFrameRendered() => FrameRendered?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class FakeDispatcherMetrics : IDispatcherMetrics
    {
        public double FramesPerSecond => 0;

        public TimeSpan AverageLatency => TimeSpan.Zero;

        public int QueueLength => 0;

        public long DroppedFrameCount => 0;

        public event EventHandler? MetricsUpdated;
    }

    private sealed class FakeOnvifStreamUriResolver(OnvifStreamUriResolution result) : IOnvifStreamUriResolver
    {
        public OnvifStreamUriResolution Resolve(
            EntityCamera camera,
            VSP.Domain.Entities.CameraCredentials credentials) => result;
    }

    private sealed class ThrowingOnvifStreamUriResolver : IOnvifStreamUriResolver
    {
        public OnvifStreamUriResolution Resolve(
            EntityCamera camera,
            VSP.Domain.Entities.CameraCredentials credentials) =>
            throw new InvalidOperationException("ONVIF resolver must not be invoked for a non-ONVIF camera.");
    }
}
