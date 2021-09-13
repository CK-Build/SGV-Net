using CK.Core;
using System;
using System.Threading.Tasks;

namespace CodeCake.Abstractions
{
    public interface ISolution
    {
        /// <summary>
        /// Try to clean the folder, for example by deleting bin & obj.
        /// </summary>
        Task<bool> Clean( IActivityMonitor m );

        /// <summary>
        /// Build the solution.
        /// </summary>
        Task<bool> Build( IActivityMonitor m );

        /// <summary>
        /// Run the unit tests of the solution.
        /// </summary>
        Task<bool> Test( IActivityMonitor m );
    }
}
