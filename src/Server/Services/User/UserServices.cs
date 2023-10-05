﻿namespace Services;

public partial class UserServices : BaseServices, IUserServices, IRegisterAsScoped
{
	#region Fields
	[AutoInject] private readonly IEasyCachingProvider _cache;
	[AutoInject] private readonly ITokenServices _tokenServices;
	[AutoInject] private readonly ILogger<UserServices> _logger;
	[AutoInject] private readonly IUserRepository _userRepository;
	[AutoInject] private readonly UserManager<User> _userManager;
	[AutoInject] private readonly RoleManager<Role> _roleManager;
	[AutoInject] private readonly IOptions<ApplicationSettings> _options;
	[AutoInject] private readonly DatabaseContext _databaseContext;
	#endregion /Fields

	#region Public Methods
	/// <summary>
	/// Ban or UnBan user by userId
	/// </summary>
	/// <param name="userId"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result> ToggleBanUser(int userId)
	{
		var result = new Result();

		var foundedUser =
			await _userManager.FindByIdAsync(userId: userId.ToString());

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage, MessageCode.HttpNotFoundError);

			return result;
		}

		if (foundedUser.IsSystemic)
		{
			var errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDenied);

			result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

			return result;
		}

		foundedUser.IsBanned = !foundedUser.IsBanned;

		await _userRepository.DeleteUserTokens(userId: foundedUser.Id);

		await RemoveUserLoggedInFromCache(userId);

		string successMessage =
			foundedUser.IsBanned == true
				?
				string.Format(Resources.Messages.SuccessMessages.UserBannedSuccessful)
				:
				string.Format(Resources.Messages.SuccessMessages.UserUnbannedSuccessful);

		result.AddSuccessMessage(successMessage);

		if (_logger.IsWarningEnabled)
			_logger.LogWarning(successMessage, parameters: new List<object?>
			{
				userId
			});

		return result;
	}


	/// <summary>
	/// Set user IsDelete field to true in database
	/// </summary>
	/// <param name="userId"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result> UserSoftDeleteAsync(int userId)
	{
		var result = new Result();

		var foundedUser =
			await _userManager.FindByIdAsync(userId.ToString());

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage, MessageCode.HttpNotFoundError);

			return result;
		}

		if (foundedUser.IsSystemic)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDenied);

			result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

			return result;
		}

		foundedUser.IsDeleted = true;

		await _userRepository.DeleteUserTokens(userId: foundedUser.Id);

		await RemoveUserLoggedInFromCache(foundedUser.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.DeleteUserSuccessful);

		result.AddSuccessMessage(successMessage);

		if (_logger.IsWarningEnabled)
			_logger.LogWarning(Resources.Messages.SuccessMessages.DeleteSuccessful, parameters: new List<object?>
			{
				userId
			});

		return result;
	}


	/// <summary>
	/// Remove user token in database
	/// </summary>
	public async Task<Result> LogoutAsync(int userTokenId, int userId)
	{
		var result = new Result();

		await _userRepository.DeleteUserToken(userTokenId);

		await RemoveUserLoggedInFromCache(userId);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.LogoutSuccessful);

		result.AddSuccessMessage(successMessage);

		return result;
	}


	/// <summary>
	/// Update a user information in database by admin
	/// </summary>
	/// <param name="requestViewModel"></param>
	/// <param name="adminId"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result> UpdateUserByAdminAsync
		(UpdateUserByAdminRequestViewModel requestViewModel, int adminId)
	{
		var result = new Result();

		var adminUser =
			await _userManager.FindByIdAsync(adminId.ToString());

		if (adminUser == null)
		{
			var errorMessage =
				string.Format(nameof(HttpStatusCode.Unauthorized));

			result.AddErrorMessage(errorMessage, MessageCode.HttpUnauthorizeError);

			return result;
		}

		var adminRoles =
			await _userManager.GetRolesAsync(adminUser);

		if (adminRoles == null || adminRoles.Count == 0)
		{
			var errorMessage =
				string.Format(nameof(HttpStatusCode.Unauthorized));

			result.AddErrorMessage(errorMessage, MessageCode.HttpUnauthorizeError);

			return result;
		}

		var user =
			await _userManager.FindByIdAsync(requestViewModel.UserId.ToString()!);

		if (user == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage, MessageCode.HttpNotFoundError);

			return result;
		}

		var userRoles =
			await _userManager.GetRolesAsync(user);

		if (user.Id != adminId)
		{
			if (!adminUser.IsSystemic && userRoles[0] == adminRoles[0])
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForUpdateThisUser);

				result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

				return result;
			}

			if (adminRoles[0] == Constants.Role.Admin && userRoles[0] == Constants.Role.SystemAdmin)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForUpdateThisUser);

				result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

				return result;
			}
		}

		if (user.UserName != requestViewModel.UserName)
		{
			var isUserNameExist =
				await _userRepository.CheckUserNameExist
					(username: _userManager.NormalizeName(requestViewModel.UserName));

			if (isUserNameExist)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.UserNameExist);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}

		if (user.Email != requestViewModel.Email)
		{
			var isEmailExist =
				await _userRepository.CheckEmailExist
					(email: _userManager.NormalizeEmail(requestViewModel.Email));

			if (isEmailExist)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.EmailExist);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}

		user.Email = requestViewModel.Email;
		user.UserName = requestViewModel.UserName;
		user.FullName = requestViewModel.FullName;

		await _userRepository.DeleteUserTokens(userId: user.Id);

		await RemoveUserLoggedInFromCache(user.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.UpdateSuccessful);

		result.AddSuccessMessage(successMessage);

		if (_logger.IsWarningEnabled)
			_logger.LogWarning(Resources.Messages.SuccessMessages.UpdateSuccessful, parameters: new List<object?>
			{
				requestViewModel
			});

		return result;
	}


	/// <summary>
	/// Login a user by username and password
	/// </summary>
	/// <param name="requestViewModel"></param>
	/// <param name="ipAddress"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result<LoginResponseViewModel>>
		LoginAsync(LoginRequestViewModel requestViewModel, string? ipAddress)
	{
		var result =
			new Result<LoginResponseViewModel>();

		var foundedUser =
			await GetUserByName(requestViewModel.UserName!);

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidUserAndOrPass);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var isPasswordValid =
			await CheckPasswordValid(user: foundedUser, password: requestViewModel.Password!);

		if (!isPasswordValid)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidUserAndOrPass);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (foundedUser.IsBanned)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserBanned);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var tokenExpiredTime =
			Domain.SeedWork.Utilities.DateTimeOffsetNow
			.AddDays(_options.Value.JwtSettings?.TokenExpiresTime ?? 30);

		var userTokenId =
			await InitializeAccessTokenInDb(userId: foundedUser.Id,
			token: "-",
			expireIn: tokenExpiredTime,
			ip: ipAddress);

		var claimsIdentity =
			await CreateClaimsIdentity(user: foundedUser, userTokenId: userTokenId);

		var accessToken =
			CreateAccessToken(claimsIdentity: claimsIdentity, expireIn: tokenExpiredTime);

		await UpdateAccessTokenInDB(id: userTokenId, token: accessToken);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.LoginSuccessful);

		result.AddSuccessMessage(successMessage);

		var response =
			new LoginResponseViewModel()
			{
				Token = accessToken,
				UserName = foundedUser.UserName,
			};

		result.Value = response;

		await AddUserLoggedInToCache(foundedUser.Id);

		if (_logger.IsWarningEnabled)
			_logger.LogInformation(Resources.Resource.UserLoginSuccessfulInformation, parameters: new List<object?>
			{
				new
				{
					requestViewModel.UserName,
				}
			});

		return result;
	}


	/// <summary>
	/// Create a new user in database
	/// </summary>
	/// <param name="registerRequestViewModel"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result>
		RegisterAsync(RegisterRequestViewModel registerRequestViewModel)
	{
		var result = new Result();

		var registerUser =
			new User(registerRequestViewModel.UserName!)
			{
				Email = registerRequestViewModel.Email,
			};

		var identityUserResult =
			await _userManager.CreateAsync(registerUser, registerRequestViewModel.Password!);

		if (!identityUserResult.Succeeded)
		{
			foreach (var error in identityUserResult.Errors)
			{
				result.AddErrorMessage(error.Description);
			}

			return result;
		}

		var identityRoleResult =
			await _userManager.AddToRoleAsync(registerUser, Constants.Role.User);

		if (!identityRoleResult.Succeeded)
		{
			foreach (var error in identityUserResult.Errors)
			{
				result.AddErrorMessage(error.Description);
			}

			return result;
		}

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.RegisterSuccessful);

		result.AddSuccessMessage(successMessage);

		return result;
	}


	/// <summary>
	/// Change user role by admin
	/// </summary>
	/// <param name="requestViewModel"></param>
	/// <param name="adminId"></param>
	/// <returns>Success or Failed Result</returns>
	public async Task<Result> ChangeUserRoleAsync
		(ChangeUserRoleRequestViewModel requestViewModel, int adminId)
	{
		var result = new Result();

		var adminUser =
			await _userManager.FindByIdAsync(adminId.ToString());

		if (adminUser == null)
		{
			var errorMessage =
				string.Format(nameof(HttpStatusCode.Unauthorized));

			result.AddErrorMessage(errorMessage, MessageCode.HttpUnauthorizeError);

			return result;
		}

		var isRoleExist =
			await _roleManager.RoleExistsAsync(requestViewModel.RoleName);

		if (!isRoleExist)
		{
			var errorMessage = string.Format
				(Resources.Messages.ErrorMessages.RoleNotFound);

			result.AddErrorMessage(errorMessage, MessageCode.HttpNotFoundError);

			return result;
		}

		var adminRoles =
			await _userManager.GetRolesAsync(adminUser);

		var foundedUser =
			await _userManager.FindByIdAsync
				(userId: requestViewModel.UserId!.Value.ToString());

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage, MessageCode.HttpNotFoundError);

			return result;
		}

		var userRoles =
			await _userManager.GetRolesAsync(foundedUser);

		if (adminRoles.Count == 0 || userRoles.Count == 0)
		{
			var errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDeniedForChangeRole);

			result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

			return result;
		}

		if (foundedUser.Id == adminId)
		{
			var errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDeniedForChangeRole);

			result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

			return result;
		}

		if (foundedUser.IsSystemic)
		{
			var errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDeniedForChangeRole);

			result.AddErrorMessage(errorMessage, MessageCode.HttpForbidenError);

			return result;
		}

		foreach (var role in userRoles)
		{
			await _userManager.RemoveFromRoleAsync(foundedUser, role);
		}

		var addToRoleResult =
			await _userManager.AddToRoleAsync(foundedUser, requestViewModel.RoleName);

		if (!addToRoleResult.Succeeded)
		{
			foreach (var error in addToRoleResult.Errors)
			{
				result.AddErrorMessage(error.Description);
			}

			return result;
		}

		await _userRepository.DeleteUserTokens(userId: foundedUser.Id);

		await RemoveUserLoggedInFromCache(foundedUser.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.UpdateSuccessful);

		result.AddSuccessMessage(successMessage);

		if (_logger.IsWarningEnabled)
			_logger.LogWarning(Resources.Messages.SuccessMessages.UpdateSuccessful, parameters: new List<object?>
			{
				requestViewModel
			});

		return result;
	}
	#endregion /Public Methods

	#region Private Methods
	/// <summary>
	/// Generate new refresh token
	/// </summary>
	private string CreateAccessToken(ClaimsIdentity claimsIdentity, DateTimeOffset expireIn)
	{
		var accessToken =
			_tokenServices.GenerateJwtToken
				(securityKey: _options.Value.JwtSettings!.SecretKeyForToken,
				claimsIdentity: claimsIdentity,
				dateTime: expireIn);

		return accessToken;
	}


	private async Task<User?> GetUserByName(string name)
	{
		return await _userManager.FindByNameAsync(name);
	}


	private async Task<bool> CheckPasswordValid(string password, User user)
	{
		return await _userManager.CheckPasswordAsync(user: user, password: password);
	}


	private async Task<int> InitializeAccessTokenInDb
		(int userId, string token, DateTimeOffset expireIn, string? ip)
	{
		var userToken = new UserToken
		{
			AccessToken = token,
			ExpireDate = expireIn,
			CreatedByIp = ip,
			UserId = userId,
		};

		await _userRepository.AddUserTokenAsync(userToken);

		await _userRepository.SaveChangesAsync();

		return userToken.Id;
	}


	private async Task<int> UpdateAccessTokenInDB(int id, string token)
	{
		var rowUpdatedCount = await _databaseContext.UserAccessTokens!
			.Where(current => current.Id == id)
			.ExecuteUpdateAsync(current => current.SetProperty(current => current.AccessToken, token));

		return rowUpdatedCount;
	}


	private async Task<ClaimsIdentity> CreateClaimsIdentity(User user, int userTokenId)
	{
		var userRoles =
			await _userManager.GetRolesAsync(user);

		var claims = new List<Claim>()
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()!),
			new Claim(ClaimTypes.Name, user.UserName!),
			new Claim(ClaimTypes.Email, user.Email!),
			new Claim(Constants.Authentication.UserTokenId, userTokenId.ToString()),
			new Claim(nameof(User.SecurityStamp), user.SecurityStamp!),
		};

		foreach (var userRole in userRoles)
		{
			claims.Add(new Claim(ClaimTypes.Role, userRole));
		}

		var claimIdentity = new ClaimsIdentity(claims);

		return claimIdentity;
	}


	private async Task AddUserLoggedInToCache(int userId)
	{
		await _cache.TrySetAsync
			($"user-Id-{userId}-logged-in", true, TimeSpan.FromHours(_options.Value.JwtSettings.UserTimeInCache));
	}


	private async Task RemoveUserLoggedInFromCache(int userId)
	{
		await _cache.RemoveByPrefixAsync($"user-Id-{userId}");
	}
	#endregion /Private Methods
}
