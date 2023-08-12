﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Softmax.Results;

public static class ResultExtensions
{
	static ResultExtensions()
	{
	}


	public static ObjectResult ApiResult(this Result result)
	{
		ObjectResult objectResult = new OkObjectResult(result);

		if (result.IsSuccess)
		{
			objectResult = new OkObjectResult(result);
		}

		switch (result.MessageCode)
		{
			case (int) MessageCode.HttpBadRequestCode:
				objectResult = new BadRequestObjectResult(result);
				break;

			case (int) MessageCode.HttpNotFoundError:
				objectResult = new NotFoundObjectResult(result);
				break;

			case (int) MessageCode.HttpServerError:
				objectResult = new ObjectResult(result);
				objectResult.StatusCode = (int) MessageCode.HttpServerError;
				break;
		}

		return objectResult;
	}

	public static ObjectResult ApiResultWithPagination<T>
		(this PaginationResult<T> result, IHeaderDictionary keyValuePairs)
	{
		var normalResult =
			new Result<T>()
			{
				IsSuccess = result.IsSuccess,
				MessageCode = result.MessageCode,
				Messages = result.Messages,
				Value = result.Value,
			};

		ObjectResult objectResult = new OkObjectResult(normalResult);

		if (result.IsSuccess)
		{
			objectResult = new OkObjectResult(normalResult);
		}

		switch (normalResult.MessageCode)
		{
			case (int) MessageCode.HttpBadRequestCode:
				objectResult = new BadRequestObjectResult(normalResult);
				break;

			case (int) MessageCode.HttpNotFoundError:
				objectResult = new NotFoundObjectResult(normalResult);
				break;

			case (int) MessageCode.HttpServerError:
				objectResult = new ObjectResult(normalResult);
				objectResult.StatusCode = (int) MessageCode.HttpServerError;
				break;
		}

		keyValuePairs.Add
			("X-Pagination", JsonSerializer.Serialize(result.Pagination, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			}));

		return objectResult;
	}
}
