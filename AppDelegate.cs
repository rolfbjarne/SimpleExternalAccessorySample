using System;

using ExternalAccessory;
using Foundation;
using UIKit;

using MonoTouch.Dialog;

namespace SimpleExternalAccessorySample
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate, INSStreamDelegate
	{
		UIWindow window;
		DialogViewController dvc;
		EntryElement entry;
		StringElement result;

		EAAccessory [] accessoryList;
		EASession session;
		long totalBytesRead;

		public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
		{
			window = new UIWindow (UIScreen.MainScreen.Bounds);

			dvc = new DialogViewController (
				new RootElement ("Root")
				{
					new Section ("Main") {
						(entry = new EntryElement ("Protocol", "com.diagsys.consbt", "com.diagsys.consbt")),
						new StyledStringElement ("Connect", Connect),
						new StyledStringElement ("Disconnect", Disconnect),
						(result = new StringElement ("Bytes read")),
					},
				}
			);

			window.RootViewController = dvc;
			window.MakeKeyAndVisible ();

			return true;
		}

		void Connect ()
		{
			if (accessoryList == null)
				accessoryList = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories;

			if (session != null) {
				result.Caption = "Disconnect first";
				dvc.ReloadData ();
				return;
			}

			var protocol = entry.Value;
			foreach (var accessory in accessoryList) {
				if (accessory.ProtocolStrings [0] != protocol)
					continue;

				session = new EASession (accessory, protocol);
				break;
			}

			if (session == null) {
				result.Caption = string.Format ("Protocol '{0}' not found in {1} accessories", protocol, accessoryList.Length);
				dvc.ReloadData ();
				return;
			}

			session.InputStream.Delegate = this;
			session.InputStream.Schedule (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			session.InputStream.Open ();

			session.OutputStream.Delegate = this;
			session.OutputStream.Schedule (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			session.OutputStream.Open ();
		}

		void Disconnect ()
		{
			if (session == null) {
				result.Caption = "Not connected.";
				dvc.ReloadData ();
				return;
			}

			result.Caption = string.Format ("Bytes received from session: {0}", totalBytesRead);
			dvc.ReloadData ();

			session.InputStream.Close ();
			session.InputStream.Unschedule (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			session.InputStream.Delegate = null;
			session.InputStream.Dispose ();

			session.OutputStream.Close ();
			session.OutputStream.Unschedule (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			session.OutputStream.Delegate = null;
			session.OutputStream.Dispose ();

			session.Dispose ();
			session = null;
			totalBytesRead = 0;
		}

		void ReadReceivedData ()
		{
			byte[] readData = new byte [128];

			while (session.InputStream.HasBytesAvailable ())
				totalBytesRead += session.InputStream.Read (readData, (nuint) readData.Length);
		}

		[Export ("stream:handleEvent:")]
		public void HandleEvent (NSStream theStream, NSStreamEvent streamEvent)
		{
			switch (streamEvent) {
			case NSStreamEvent.None:
			case NSStreamEvent.OpenCompleted:
				break;
			case NSStreamEvent.HasBytesAvailable:
				ReadReceivedData ();
				break;
			case NSStreamEvent.HasSpaceAvailable:
			case NSStreamEvent.ErrorOccurred:
			case NSStreamEvent.EndEncountered:
			default:
				break;
			}
		}
	}
}
