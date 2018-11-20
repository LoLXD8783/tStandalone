﻿using System;
using System.IO;
using System.Net;
using System.Net.Security;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Terraria.ModLoader.UI
{
	internal class UIDownloadFile : UIState
	{
		private UILoadProgress loadProgress;
		private string name;
		private string url;
		private string file;
		private Action success;
		private Action failure;
		private Action cancelAction;
		private WebClient client;

		public override void OnInitialize()
		{
			loadProgress = new UILoadProgress();
			loadProgress.Width.Set(0f, 0.8f);
			loadProgress.MaxWidth.Set(600f, 0f);
			loadProgress.Height.Set(150f, 0f);
			loadProgress.HAlign = 0.5f;
			loadProgress.VAlign = 0.5f;
			loadProgress.Top.Set(10f, 0f);
			base.Append(loadProgress);

			var cancel = new UITextPanel<string>(Language.GetTextValue("UI.Cancel"), 0.75f, true);
			cancel.VAlign = 0.5f;
			cancel.HAlign = 0.5f;
			cancel.Top.Set(170f, 0f);
			cancel.OnMouseOver += UICommon.FadedMouseOver;
			cancel.OnMouseOut += UICommon.FadedMouseOut;
			cancel.OnClick += CancelClick;
			base.Append(cancel);
		}

		public override void OnActivate()
		{
			loadProgress.SetText(Language.GetTextValue("tModLoader.MBDownloadingMod", name));
			loadProgress.SetProgress(0f);
			if (!UIModBrowser.PlatformSupportsTls12)
			{
				Interface.errorMessage.SetMessage("TLS 1.2 not supported on this computer."); // github releases
				Interface.errorMessage.SetGotoMenu(0);
				Main.gameMenu = true;
				Main.menuMode = Interface.errorMessageID;
				return;
			}
			if (UIModBrowser.PlatformSupportsTls12) // Needed for downloads from Github
			{
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // SecurityProtocolType.Tls12
			}
			client = new WebClient();
			ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback((sender, certificate, chain, policyErrors) => { return true; });
			SetCancel(client.CancelAsync);
			client.DownloadProgressChanged += Client_DownloadProgressChanged;
			client.DownloadFileCompleted += Client_DownloadFileCompleted;
			client.DownloadFileAsync(new Uri(url), file);
		}

		private void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				if (e.Cancelled)
				{
					Main.menuMode = 0;
				}
				else
				{
					// TODO: Think about what message to put here.
					HttpStatusCode httpStatusCode = GetHttpStatusCode(e.Error);
					if (httpStatusCode == HttpStatusCode.ServiceUnavailable)
					{
						Interface.errorMessage.SetMessage(Language.GetTextValue("tModLoader.MBExceededBandwidth"));
						Interface.errorMessage.SetGotoMenu(0);
						Main.gameMenu = true;
						Main.menuMode = Interface.errorMessageID;
					}
					else
					{
						Interface.errorMessage.SetMessage(Language.GetTextValue("tModLoader.MBUnknownMBError"));
						Interface.errorMessage.SetGotoMenu(0);
						Main.gameMenu = true;
						Main.menuMode = Interface.errorMessageID;
					}
				}
				if (File.Exists(file))
					File.Delete(file);
			}
			else if (!e.Cancelled)
			{
				client.Dispose();
				client = null;
				success();
			}
			else
			{
				if (File.Exists(file))
					File.Delete(file);
			}
		}

		private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			SetProgress(e);
		}

		internal void SetDownloading(string name, string url, string file, Action success)
		{
			this.name = name;
			this.url = url;
			this.file = file;
			this.success = success;
		}

		public void SetCancel(Action cancelAction)
		{
			this.cancelAction = cancelAction;
		}

		internal void SetProgress(DownloadProgressChangedEventArgs e) => SetProgress(e.BytesReceived, e.TotalBytesToReceive);
		internal void SetProgress(long count, long len)
		{
			loadProgress?.SetProgress((float)count / len);
		}

		private void CancelClick(UIMouseEvent evt, UIElement listeningElement)
		{
			Main.PlaySound(SoundID.MenuOpen);
			cancelAction?.Invoke();
		}

		private HttpStatusCode GetHttpStatusCode(System.Exception err)
		{
			if (err is WebException)
			{
				WebException we = (WebException)err;
				if (we.Response is HttpWebResponse)
				{
					HttpWebResponse response = (HttpWebResponse)we.Response;
					return response.StatusCode;
				}
			}
			return 0;
		}
	}
}
