﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Content.Data.Converters;
using VirtoCommerce.Content.Data.Models;
using VirtoCommerce.Content.Data.Repositories;
using VirtoCommerce.Content.Data.Utility;

namespace VirtoCommerce.Content.Data.Services
{
	public class ThemeServiceImpl : IThemeService
	{
		private readonly Func<IContentRepository> _repositoryFactory;

		public ThemeServiceImpl(Func<IContentRepository> repositoryFactory)
		{
			if (repositoryFactory == null)
				throw new ArgumentNullException("repositoryFactory");

			_repositoryFactory = repositoryFactory;
		}

		public ThemeServiceImpl(Func<IContentRepository> repositoryFactory, string tempPath)
		{
			if (repositoryFactory == null)
				throw new ArgumentNullException("repositoryFactory");

			_repositoryFactory = repositoryFactory;
		}

		public IEnumerable<Theme> GetThemes(string storeId)
		{
			var themePath = GetThemePath(storeId, string.Empty);

			using (var repository = _repositoryFactory())
			{
				var items = repository.GetThemes(themePath);
				return items;
			}
		}

		public void DeleteTheme(string storeId, string themeId)
		{
			var themePath = GetThemePath(storeId, themeId);

			using (var repository = _repositoryFactory())
			{
				repository.DeleteTheme(themePath);
			}
		}

		public IEnumerable<ThemeAsset> GetThemeAssets(string storeId, string themeName, GetThemeAssetsCriteria criteria)
		{
			var themePath = GetThemePath(storeId, themeName);
			using (var repository = _repositoryFactory())
			{
				var items = repository.GetContentItems(themePath, criteria);
				var retVal = new List<ThemeAsset>();

				foreach (var item in items)
				{
					var path = item.Path;
					item.Path = FixPath(themePath, item.Path);
					item.ContentType = ContentTypeUtility.GetContentType(item.Name, item.ByteContent);
					var addedItem = item.AsThemeAsset();
					addedItem.Path = path;
					retVal.Add(addedItem);
				}

				return retVal;
			}
		}

		public ThemeAsset GetThemeAsset(string storeId, string themeId, string path)
		{
			var fullPath = GetFullPath(storeId, themeId, path);
			using (var repository = _repositoryFactory())
			{
				var item = repository.GetContentItem(fullPath);

				item.Path = FixPath(GetThemePath(storeId, themeId), item.Path);
				item.ContentType = ContentTypeUtility.GetContentType(item.Name, item.ByteContent);

				return item.AsThemeAsset();
			}
		}

		public void SaveThemeAsset(string storeId, string themeId, Models.ThemeAsset asset)
		{

			var fullPath = GetFullPath(storeId, themeId, asset.Id);
			using (var repository = _repositoryFactory())
			{
				repository.SaveContentItem(fullPath, asset.AsContentItem());
			}
		}

		public void DeleteThemeAssets(string storeId, string themeId, params string[] assetIds)
		{
			using (var repository = _repositoryFactory())
			{
				foreach (var assetId in assetIds)
				{
					var fullPath = GetFullPath(storeId, themeId, assetId);

					repository.DeleteContentItem(fullPath);
				}
			}
		}

		public void UploadTheme(string storeId, string themeName, System.IO.Compression.ZipArchive archive)
		{
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (!IsFolder(entry))
				{
					using (var stream = entry.Open())
					{
						var asset = new ThemeAsset
						{
							AssetName = PrepareAssetNameAndId(entry.FullName),
							Id = PrepareAssetNameAndId(entry.FullName)
						};

						var arr = ReadFully(stream);
						asset.ByteContent = arr;
						asset.ContentType = ContentTypeUtility.GetContentType(entry.FullName, arr);

						SaveThemeAsset(storeId, themeName.Trim('/'), asset);
					}
				}
			}
		}

		public void CreateDefaultTheme(string storeId, string themePath)
		{
			var themesPath = GetThemePath(storeId, string.Empty);
			using (var repository = _repositoryFactory())
			{
				var themes = repository.GetThemes(themesPath);
				if (!themes.Any() || !string.IsNullOrEmpty(themePath))
				{
					var files = Directory.GetFiles(themePath, "*.*", SearchOption.AllDirectories);

					var items =
						files.Select(
							file => new ContentItem { Name = Path.GetFileName(file), Path = RemoveBaseDirectory(file, themePath), ModifiedDate = File.GetLastWriteTimeUtc(file) })
							.ToList();

					foreach (var contentItem in items)
					{
						var fullFile = GetContentItem(contentItem.Path, storeId, themePath);
						contentItem.Id = Guid.NewGuid().ToString();
						contentItem.ByteContent = fullFile.ByteContent;
						contentItem.ContentType = fullFile.ContentType;
						contentItem.Path = string.Format("{0}/{1}", storeId, contentItem.Path);
						contentItem.CreatedDate = DateTime.UtcNow;
						contentItem.ModifiedDate = DateTime.UtcNow;

						repository.SaveContentItem(contentItem.Path, contentItem);
					}
				}
			}
		}

		private string GetThemePath(string storeId, string themeName)
		{
			if (string.IsNullOrEmpty(themeName))
			{
				return string.Format("{0}/", storeId);
			}
			return string.Format("{0}/{1}", storeId, themeName);
		}

		private string GetFullPath(string storeId, string themeName, string path)
		{
			return string.Format("{0}/{1}/{2}", storeId, themeName, path);
		}

		private string FixPath(string themePath, string path)
		{
			return path.ToLowerInvariant().Replace(themePath.ToLowerInvariant(), string.Empty).Trim('/');
		}

		private static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[16 * 1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, read);
				}
				return ms.ToArray();
			}
		}

		private bool IsFolder(ZipArchiveEntry entry)
		{
			return entry.FullName.EndsWith("/");
		}

		private string PrepareAssetNameAndId(string fullName)
		{
			var retVal = string.Empty;

			var steps = fullName.Split('/');

			if(_defaultFolders.Contains(steps[0]) && steps.Length > 1)
			{
				retVal = fullName;
			}
			else if (!_defaultFolders.Contains(steps[0]) && steps.Length == 1)
			{
				retVal = fullName;
			}
			else
			{
				retVal = string.Join("/", steps.Skip(1));
			}

			return retVal;
		}

		private ContentItem GetContentItem(string path, string storeId, string themePath)
		{
			var retVal = new ContentItem();

			var fullPath = GetFullPath(path, themePath);

			var itemName = Path.GetFileName(fullPath);
			retVal.ByteContent = File.ReadAllBytes(fullPath);
			retVal.Name = itemName;
			retVal.ContentType = ContentTypeUtility.GetContentType(fullPath, retVal.ByteContent);

			return retVal;
		}

		private string GetFullPath(string path, string themePath)
		{
			return Path.Combine(themePath, path).Replace("/", "\\");
		}

		private string RemoveBaseDirectory(string path, string themePath)
		{
			return path.Replace(themePath, string.Empty).Replace("\\", "/").TrimStart('/');
		}

		private string[] _defaultFolders = new string[] { "assets", "layout", "templates", "snippets", "config", "locales" };

	}
}
