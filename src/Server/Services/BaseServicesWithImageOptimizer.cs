﻿using Infrastructure.Utilities;
using Softmax.Utilities.Image;

namespace Services;

public abstract class BaseServicesWithSaveFile
{
	protected readonly IFileManager _fileManager;

	public BaseServicesWithSaveFile(IFileManager fileManager) : base()
	{
		_fileManager = fileManager;
	}


	protected async Task<ApplicationFile> SaveImageWithRandomNameAsync(SaveImageSetting saveFileSetting)
	{
		using var currentImage =
			saveFileSetting.File.OpenReadStream();

		var imageOptimizer = new ImageOptimizer();

		using var optimizedImage =
			imageOptimizer.Optimize(stream: currentImage,
				optimizeSettings: new OptimizeSettings
				{
					Quality = saveFileSetting.OptimizeImage.Quality,
					Width = saveFileSetting.OptimizeImage.Width,
					ImageExtensions = saveFileSetting.OptimizeImage.ImageExtensions
				});

		var saveFileResult =
			await _fileManager.SaveStreamWithRandomeNameAsync
				(stream: optimizedImage,
				fileExtension: GetExtension(fileName: saveFileSetting.File.FileName,
					imageExtension: saveFileSetting.OptimizeImage.ImageExtensions),
				root: saveFileSetting.WebRoot,
				path: saveFileSetting.Path,
				includeWebRootPath: !string.IsNullOrWhiteSpace(saveFileSetting.WebRoot));

		if (saveFileResult == null)
			throw new ArgumentNullException(nameof(saveFileResult));

		var applicationFile = new ApplicationFile
		{
			Name = saveFileResult.FileName,
			ContentType = saveFileResult.ContentType,
			Size = saveFileResult.ByteSize,
			Extension = saveFileResult.Extension,
			Path = saveFileResult.SavedUrl,
			Description = saveFileSetting.Description,
			Ordering = saveFileSetting.Ordering,
		};

		return applicationFile;
	}


	protected async Task<ApplicationFile> SaveFileAsync(SaveFileSetting saveFileSetting)
	{
		var saveFileResult =
			await _fileManager.SaveFileWithRandomeNameAsync
				(file: saveFileSetting.File,
				root: saveFileSetting.WebRoot,
				path: saveFileSetting.Path,
				includeWebRootPath: !string.IsNullOrWhiteSpace(saveFileSetting.WebRoot));

		if (saveFileResult == null)
			throw new ArgumentNullException(nameof(saveFileResult));

		var applicationFile = new Domain.ApplicationFile
		{
			Name = saveFileResult.FileName,
			ContentType = saveFileResult.ContentType,
			Size = saveFileResult.ByteSize,
			Extension = saveFileResult.Extension,
			Path = saveFileResult.SavedUrl,
			Description = saveFileSetting.Description,
			Ordering = saveFileSetting.Ordering,
		};

		return applicationFile;
	}


	private string GetExtension(string fileName, ImageExtensions? imageExtension)
	{
		return imageExtension.ToFixedString() ??
				_fileManager.GetFileExtention(fileName) ??
				ImageExtensions.jpg.ToFixedString();
	}
}

public class SaveImageSetting
{
	public required IFormFile File { get; set; }

	public required string Path { get; set; }

	public OptimizeImage OptimizeImage { get; set; }

	public int Ordering { get; set; } = 10_000;

	public string? WebRoot { get; set; }

	public string? Description { get; set; }
}

public class SaveFileSetting
{
	public required IFormFile File { get; set; }

	public required string Path { get; set; }

	public int Ordering { get; set; } = 10_000;

	public string? WebRoot { get; set; }

	public string? Description { get; set; }
}