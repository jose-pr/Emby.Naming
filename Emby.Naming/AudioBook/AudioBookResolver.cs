﻿using System;
using System.IO;
using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.TV;
using Emby.Naming.Video;

namespace Emby.Naming.AudioBook
{
    public class AudioBookResolver
    {
        private readonly NamingOptions _options;
        private readonly IRegexProvider _iRegexProvider;

        public AudioBookResolver(NamingOptions options)
            : this(options, new RegexProvider())
        {
        }

        public AudioBookResolver(NamingOptions options, IRegexProvider iRegexProvider)
        {
            _options = options;
            _iRegexProvider = iRegexProvider;
        }

        public AudioBookFileInfo ParseFile(string path)
        {
            return Resolve(path, false);
        }

        public AudioBookFileInfo ParseDirectory(string path)
        {
            return Resolve(path, true);
        }

        public AudioBookFileInfo Resolve(string path, bool IsDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }
            if (IsDirectory)
                return null;

            var extension = Path.GetExtension(path) ?? string.Empty;
            // Check supported extensions
            if (!_options.AudioFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var container = extension.TrimStart('.');

            var parsingResult = new AudioBookFilePathParser(_options, _iRegexProvider)
                .Parse(path, IsDirectory);
            
            return new AudioBookFileInfo
            {
                Path = path,
                Container = container,
                PartNumber = parsingResult.PartNumber,
                ChapterNumber = parsingResult.ChapterNumber,
                IsDirectory = IsDirectory
            };
        }
    }
}
