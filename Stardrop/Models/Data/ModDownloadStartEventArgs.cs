using System;
using System.Threading;

namespace Stardrop.Models.Data
{
    internal record ModDownloadStartedEventArgs(Uri Uri, string Name, long? Size, CancellationToken DownloadCancelToken);
}
