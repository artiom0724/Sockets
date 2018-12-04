using ClientSocket.Helpers;
using PingSocket.Replays;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace PingSocket.Services
{
	public class PingService
	{
		public void PingIds(List<string> parameters)
		{
			foreach (var id in parameters)
			{
				new Thread(PingId).Start(id);
			}
		}

		public void PingId(object data)
		{
			var ip = (string)data;
			var replies = new List<CustomPingReply>();
			for (var i = 0; i < 4; i++)
			{
				using (var ping = new PingSocketSend())
				{
					replies.Add(ping.Send(ip));
				}
			}
			var reportFileName = $"PingReport-{Guid.NewGuid():N}.txt";
			var reportFilePath = $"{$"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}reports"}{Path.DirectorySeparatorChar}{reportFileName}";
			WriteReport(ip, replies, reportFilePath);
		}

		private void WriteReport(string host, List<CustomPingReply> replies, string filePath)
		{
			using (var file = new FileStream(filePath, FileMode.Append))
			{
				var buffer = new StringBuilder();
				var reply = replies.First();

				buffer.AppendLine("--------------------------------------------------");
				buffer.AppendLine($"Ping statistics for {host} " +
								  $"{(host == reply.Address.ToString() ? string.Empty : $"[{reply.Address}]")}:");
				buffer.AppendLine("\tPackets:");
				buffer.AppendLine($"\t\tsent = {replies.Count},");
				buffer.AppendLine($"\t\treceived = {replies.Count(r => r.Status == IPStatus.Success)},");
				buffer.AppendLine($"\t\tlost = {replies.Count(r => r.Status != IPStatus.Success)}.");
				if (replies.Count(r => r.Status == IPStatus.Success) != 0)
				{
					var times = replies.Select(r => r.ElapsedTime).ToList();

					buffer.AppendLine("\tEstimated time of reception and transmission:");
					buffer.AppendLine($"\t\tmin = {times.Min()} ms,");
					buffer.AppendLine($"\t\tmax = {times.Max()} ms,");
					buffer.AppendLine($"\t\taverage = {(int)times.Average()} ms.");
				}
				buffer.AppendLine("--------------------------------------------------");

				file.Write(buffer.ToString().StringToByteArray(), 0, buffer.Length);
			}
		}
	}
}
