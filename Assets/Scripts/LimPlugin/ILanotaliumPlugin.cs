using System.Collections;
using System.Collections.Generic;
using EasyRequest;
using UnityEngine;

namespace Lanotalium.Plugin
{
    public enum Language
    {
        // Value 0 used to be Simplified Chinese and has been removed.
        // The slot is deliberately left empty instead of renumbering:
        // plugins built against the old API baked English in as 1, so
        // shifting the values would silently break every existing plugin.
        English = 1,
        Spanish = 2
    }
    public class LanotaliumContext
    {
        /// <summary>
        /// Whether a project is currently loaded.
        /// </summary>
        public bool IsProjectLoaded { get; set; }
        /// <summary>
        /// The language the editor is running in.
        /// </summary>
        public Language CurrentLanguage { get; set; }
        /// <summary>
        /// The project currently open.
        /// </summary>
        public Project.LanotaliumProject CurrentProject { get; set; }
        /// <summary>
        /// Game logic manager.
        /// </summary>
        public LimTunerManager TunerManager { get; set; }
        /// <summary>
        /// Editor manager.
        /// </summary>
        public LimEditorManager EditorManager { get; set; }
        /// <summary>
        /// Chart editing manager.
        /// </summary>
        public LimOperationManager OperationManager { get; set; }
        /// <summary>
        /// Asks the user to input data.
        /// </summary>
        public EasyRequestManager UserRequest { get; set; }
        /// <summary>
        /// Shows a message.
        /// </summary>
        public MessageBoxManager MessageBox { get; set; }
        /// <summary>
        /// Whether execution succeeded.
        /// </summary>
        public bool Succeed { get; set; }
        /// <summary>
        /// Execution result.
        /// </summary>
        public string ProcessResult { get; set; }
    }
    public interface ILanotaliumPlugin
    {
        /// <summary>
        /// Localized plugin name.
        /// </summary>
        /// <param name="language">Language to return the name in</param>
        /// <returns>Plugin name</returns>
        string Name(Language language);
        /// <summary>
        /// Localized plugin description.
        /// </summary>
        /// <param name="language">Language to return the description in</param>
        /// <returns>Plugin description</returns>
        string Description(Language language);
        /// <summary>
        /// Runs the plugin (coroutine).
        /// </summary>
        /// <param name="context">Lanotalium context</param>
        IEnumerator Process(LanotaliumContext context);
    }
}
