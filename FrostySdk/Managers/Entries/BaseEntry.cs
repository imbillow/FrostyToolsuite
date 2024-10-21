using System.Runtime.InteropServices;
using System.Threading;

namespace FrostySdk.Managers.Entries
{
    public class BaseEntry
    {
        private static int _idCounter = 0;

        public readonly int Id = Interlocked.Increment(ref _idCounter);
    }
}