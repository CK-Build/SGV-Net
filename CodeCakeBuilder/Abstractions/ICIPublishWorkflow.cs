using CK.Core;
using System.Threading.Tasks;

namespace CodeCake.Abstractions
{
    interface ICIPublishWorkflow
    {
        /// <summary>
        /// Pack the solution: it produce the artifacts.
        /// </summary>
        Task<bool> Pack( IActivityMonitor m );

        ArtifactType ArtifactType { get; }
    }
}
