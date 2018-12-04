using ClientSocket.Helpers;
using PingSocket.Replays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PingSocket.Services
{
	public class TracertService
	{
		public void TracertIds(List<string> parameters)
		{
			new Thread(TracertId).Start(parameters);
		}

		public void TracertId(object data)
		{
			var parameters = (List<string>)data;
			var reportFileName = $"TacertReport-{Guid.NewGuid():N}.txt";
			var reportFilePath = $"{$"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}reports"}{Path.DirectorySeparatorChar}{reportFileName}";

			WriteReport(parameters.First(), new TracertSocketSend().Send(parameters.First(), (int.Parse(parameters.Last()))), reportFilePath);
		}

		private void WriteReport(string host, Dictionary<int, List<TracertPingReply>> replies, string filePath)
		{
			using (var file = new FileStream(filePath, FileMode.Append))
			{
				var buffer = new StringBuilder();

				buffer.AppendLine($"Tracert statistics for {host}:");
				foreach (var reply in replies)
				{
					buffer.Append($"[{reply.Key}]");

					buffer.Append($"\r\n\t{string.Join($"\r\n\t", reply.Value.Select(x=>x.Address.ToString() + "\t" + x.Time + " ms"))}\r\n");
				}

				file.Write(buffer.ToString().StringToByteArray(), 0, buffer.Length);
			}
		}

		private (string Host, int HopsCount) ParseArgs(List<string> parameters)
		{
			if (parameters.Count < 2)
			{
				throw new Exception("Not enough args to start tool.");
			}
			if (!int.TryParse(parameters[1], out var hopsCount))
			{
				throw new Exception("Hops count isn't valid.");
			}
			return (parameters[0], hopsCount);
		}
	}
}
