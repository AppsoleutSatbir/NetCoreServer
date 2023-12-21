using System;
using System.IO;
using System.Threading;

namespace NetCoreServer
{
	internal static class FileUtilities
	{
		static SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
		const string FATAL_FILE_PATH = "../FatalErrors/{FOLDER_DATE}/{FILE_DATE}.log";

		public static void Write(string a_log)
		{
			try
			{
				Lock.Wait();
				DateTime l_now = DateTime.Now;
				string l_pathDate = l_now.ToString("yyyyMMdd");
				string l_finalPath = FATAL_FILE_PATH.Replace("{FOLDER_DATE}", l_pathDate);
				l_finalPath = l_finalPath.Replace("{FILE_DATE}", l_pathDate + l_now.ToString("HH"));
				File.AppendAllText(l_finalPath, l_now.ToString("yyyy-MM-dd HH:mm::ss.ffff") + "::" + a_log + Environment.NewLine);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			finally
			{
				Lock.Release();
			}
		}
	}
}
