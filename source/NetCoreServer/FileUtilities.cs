using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NetCoreServer;

public static class FileUtilities
{
	private const string FATAL_FILE_PATH = "../FatalErrors/{FOLDER_DATE}/{FILE_DATE}.log";

	private static readonly Channel<string> m_channel = Channel.CreateUnbounded<string>(
		new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

	public static void Initialize()
	{
		// Start the async background processor.
		_ = ProcessChannelAsync();
	}

	private static async Task ProcessChannelAsync()
	{
		try
		{
			await foreach (string l_log in m_channel.Reader.ReadAllAsync())
			{
				await ProcessChannel(l_log);
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	public static async Task ProcessChannel(string a_log)
	{
		try
		{
			DateTime l_now = DateTime.Now;
			string l_pathDate = l_now.ToString("yyyyMMdd");
			string l_finalPath = FATAL_FILE_PATH.Replace("{FOLDER_DATE}", l_pathDate).Replace("{FILE_DATE}", l_pathDate + l_now.ToString("HH"));

			string l_directory = Path.GetDirectoryName(l_finalPath);
			if (!string.IsNullOrEmpty(l_directory))
			{
				Directory.CreateDirectory(l_directory);
			}

			string l_message = $"{l_now:yyyy-MM-dd HH:mm:ss.ffff}::{a_log}{Environment.NewLine}";

			await File.AppendAllTextAsync(l_finalPath, l_message);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	public static bool Write(string log)
	{
		return m_channel.Writer.TryWrite(log);
	}
}