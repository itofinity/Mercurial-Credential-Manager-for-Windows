using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlassian.Bitbucket.Alm.Mercurial
{
    public class MercurialInstallation : IEquatable<MercurialInstallation>
    {
        // TODO ?
        internal const string Version4DocPath = @"Docs";

        public readonly string Path;
        public readonly KnownMercurialDistribution Version;
        private KnownMercurialDistribution distro;
        private string _doc;
        private string _mercurial;

        internal const string MercurialExeName = @"hg.exe";
        internal const string AllVersionMercurialPath = MercurialExeName;

        public MercurialInstallation(string path, KnownMercurialDistribution distro)
        {
            Path = path;
            this.distro = distro;
        }

        public string Doc
        {
            get
            {
                if (_doc == null)
                {
                    _doc = System.IO.Path.Combine(Path, CommonDocPaths[Version]);
                }
                return _doc;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly IReadOnlyDictionary<KnownMercurialDistribution, string> CommonDocPaths
    = new Dictionary<KnownMercurialDistribution, string>
    {
                { KnownMercurialDistribution.Mercurialv4, Version4DocPath },
    };

        public bool Equals(MercurialInstallation other)
        {
            if (other is MercurialInstallation)
                return this == (MercurialInstallation)other;

            return false;
        }

        internal static bool IsValid(MercurialInstallation value)
        {
            return Directory.Exists(value.Path)
              && File.Exists(value.Mercurial);
        }

        public string Mercurial
        {
            get
            {
                if (_mercurial == null)
                {
                    _mercurial = System.IO.Path.Combine(Path, AllVersionMercurialPath);
                }
                return _mercurial;
            }
        }
    }
}
