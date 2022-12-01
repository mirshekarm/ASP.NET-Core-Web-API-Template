﻿namespace Services;

public partial class UserServices : BaseServices, IUserServices
{
	#region Fields
	private readonly IMapper _mapper;
	private readonly IEasyCachingProvider _cache;
	private readonly ITokenServices _tokenServices;
	private readonly ILogger<UserServices> _logger;
	private readonly IUserRepository _userRepository;
	private readonly ApplicationSettings _applicationSettings;
	#endregion /Fields

	#region Constractor
	public UserServices
		(IMapper mapper,
		IEasyCachingProvider cache,
		ITokenServices tokenServices,
		ILogger<UserServices> logger,
		IUserRepository userRepository,
		IOptions<ApplicationSettings> options) : base()
	{
		_mapper = mapper;
		_cache = cache;
		_tokenServices = tokenServices;
		_logger = logger;
		_userRepository = userRepository;
		_applicationSettings = options.Value;
	}
	#endregion /Constractor

	#region Methods
	public async Task<Result> ToggleBanUser(long? userId)
	{
		var result = new Result();

		if (userId == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.MostNotBeNull, "UserId");

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (userId <= 0)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidUserId);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var foundedUser =
			await _userRepository.GetUserById(userId: userId.Value, isTracking: true);

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		foundedUser.IsBanned = !foundedUser.IsBanned;
		foundedUser.SecurityStamp = Guid.NewGuid();

		await _userRepository.SaveChangesAsync();

		await _cache.RemoveByPrefixAsync($"userId-{foundedUser.Id}");

		string successMessage =
			foundedUser.IsBanned == true
				?
				string.Format(Resources.Messages.SuccessMessages.UserBannedSuccessful)
				:
				string.Format(Resources.Messages.SuccessMessages.UserUnbannedSuccessful);

		result.AddSuccessMessage(successMessage);

		_logger.LogWarning(Resources.Resource.UpdateSuccessful, parameters: new List<object>
		{
			userId.Value
		});

		return result;
	}


	public async Task AddUserExistToCache<TKey>(TKey? userId)
	{
		if (userId == null)
			throw new ArgumentNullException(nameof(userId));

		await _cache.TrySetAsync
			($"userId-{userId}-exist", true, TimeSpan.FromHours(1));
	}


	private UserLogin GenerateRefreshToken(string? ipAddress)
	{
		return new UserLogin(refreshToken: Guid.NewGuid())
		{
			Expires = DateTime.UtcNow.AddDays(30),
			Created = DateTime.UtcNow,
			CreatedByIp = ipAddress
		};
	}


	public async Task<Result> UserSoftDeleteAsync(long? userId)
	{
		var result = new Result();

		if (userId == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.MostNotBeNull, "UserId");

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (userId <= 0)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidUserId);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var foundedUser =
			await _userRepository.GetUserById(userId: userId.Value, isTracking: true);

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		foundedUser.IsDeleted = true;
		foundedUser.SecurityStamp = Guid.NewGuid();

		await _userRepository.SaveChangesAsync();

		await _cache.RemoveByPrefixAsync($"userId-{foundedUser.Id}");

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.DeleteUserSuccessful);

		result.AddSuccessMessage(successMessage);

		_logger.LogWarning(Resources.Resource.DeleteSuccessful, parameters: new List<object>
		{
			userId.Value
		});

		return result;
	}


	public async Task<Result<LoginResponseViewModel>>
		RefreshTokenAsync(string refreshToken, string? ipAddress)
	{
		var result =
			 RefreshTokenValidation(refreshToken, ipAddress);

		if (result.IsFailed)
			return result;

		if (!Guid.TryParse(refreshToken, out var inputRefreshToken))
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidJwtToken);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		var userLogin =
		  await _userRepository.GetUserLoginsAsync
			(refreshToken: inputRefreshToken, includeUser: true);


		if (userLogin == null || userLogin.IsExpired)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidRefreshToken);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (userLogin.User == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (userLogin.User.IsBanned)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserBanned);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var newRefreshToken = GenerateRefreshToken(ipAddress);
		userLogin.RefreshToken = newRefreshToken.RefreshToken;
		userLogin.CreatedByIp = ipAddress;

		await _userRepository.SaveChangesAsync();

		var claims = GenerateClaims(new UserClaims
		{
			Id = userLogin.User.Id.ToString(),
			RoleName = userLogin.User?.UserRole?.Title!,
			RoleId = userLogin.User?.RoleId?.ToString()!,
			SecurityStamp = userLogin.User?.SecurityStamp.ToString()!,
		});

		var expiredTime =
			DateTime.UtcNow.AddMinutes(_applicationSettings.JwtSettings?.TokenExpiresTime ?? 15);

		string jwtToken =
			_tokenServices.GenerateJwtToken
				(securityKey: _applicationSettings.JwtSettings?.SecretKeyForToken ?? string.Empty,
				claimsIdentity: claims,
				dateTime: expiredTime);

		var response =
			new LoginResponseViewModel()
			{
				Token = jwtToken,
				Username = userLogin!.User?.Username,
				RefreshToken = newRefreshToken.RefreshToken,
			};

		result.Value = response;

		await AddUserExistToCache(userId: userLogin!.User?.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.RefreshTokenSuccessfull);

		result.AddSuccessMessage(successMessage);

		_logger.LogWarning(Resources.Resource.UserRefreshTokenSuccessfulInformation, parameters: new List<object?>
		{
			userLogin.User?.Username,
		});

		return result;
	}


	public async Task<Result> LogoutAsync(string token)
	{
		var result =
			 LogoutValidation(token);

		if (result.IsFailed)
			return result;

		if (!Guid.TryParse(token, out var inputRefreshToken))
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.InvalidRefreshToken);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		var userLogin =
		  await _userRepository.GetUserLoginsAsync
			(refreshToken: inputRefreshToken, includeUser: false);

		if (userLogin == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		_userRepository.DeleteUserLogin(userLogin);

		await _userRepository.SaveChangesAsync();

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.RefreshTokenRevoked);

		result.AddSuccessMessage(successMessage);

		return result;
	}


	public async Task<Result>
		RegisterAsync(RegisterRequestViewModel registerRequestViewModel)
	{
		var result =
			await RegisterValidation(registerRequestViewModel: registerRequestViewModel);

		if (result.IsFailed == true)
			return result;

		var user =
			_mapper.Map<User>(source: registerRequestViewModel);

		user.HashedPassword =
			Security.HashDataBySHA1(registerRequestViewModel.Password);

		user.RoleId = Constants.Role.UserRoleId;

		user.SecurityStamp = Guid.NewGuid();

		await _userRepository.AddAsync(entity: user);

		await _userRepository.SaveChangesAsync();

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.RegisterSuccessful);

		result.AddSuccessMessage(successMessage);

		return result;
	}


	public async Task<Result> UpdateUserByAdminAsync
		(UpdateUserByAdminRequestViewModel requestViewModel, int? adminId)
	{
		var result =
			UpdateUserByAdminValidation(viewModel: requestViewModel);

		if (result.IsFailed)
		{
			return result;
		}

		if (!adminId.HasValue)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.MostNotBeNull, nameof(adminId));

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var adminRoleId =
			await _userRepository.GetUserRoleAsync(adminId.Value);

		if (!adminRoleId.HasValue)
		{
			var errorMessage =
				string.Format(nameof(HttpStatusCode.Unauthorized));

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var user =
			await _userRepository.GetUserById(userId: requestViewModel.UserId, isTracking: false);

		if (user == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		if (user.Id != adminId)
		{
			if (user.RoleId == adminRoleId)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForUpdateThisUser);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}

		if (adminRoleId == (int) UserRoleEnum.Admin)
		{
			if (user.RoleId == (int) UserRoleEnum.SystemAdministrator)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForUpdateThisUser);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}


		if (user.Username != requestViewModel.Username)
		{
			var isPhoneNumberExist =
				await _userRepository.CheckUsernameExist(username: requestViewModel.Username);

			if (isPhoneNumberExist)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.PhoneNumberExist);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}

		if (user.Email != requestViewModel.Email)
		{
			var isEmailExist =
				await _userRepository.CheckEmailExist(email: requestViewModel.Email);

			if (isEmailExist)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.EmailExist);

				result.AddErrorMessage(errorMessage);

				return result;
			}
		}

		var updatedUser =
			_mapper.Map<User>(source: requestViewModel);

		updatedUser.Id = requestViewModel.UserId;
		updatedUser.SecurityStamp = Guid.NewGuid();

		_userRepository.UpdateUserByAdmin(updatedUser);

		await _userRepository.SaveChangesAsync();

		await _cache.RemoveByPrefixAsync($"userId-{updatedUser.Id}");

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.UpdateSuccessful);

		result.AddSuccessMessage(successMessage);

		_logger.LogWarning(Resources.Resource.UpdateSuccessful, parameters: new List<object>
		{
			requestViewModel
		});

		return result;
	}


	public async Task<Result<LoginResponseViewModel>>
		LoginAsync(LoginRequestViewModel loginRequestViewModel, string? ipAddress)
	{
		var result =
			LoginValidation(loginRequestViewModel: loginRequestViewModel);

		if (result.IsFailed == true)
			return result;

		var hashedPassword =
			Security.HashDataBySHA1(loginRequestViewModel.Password);

		var foundedUser =
			await _userRepository.LoginAsync(username: loginRequestViewModel.Username, hashedPassword: hashedPassword);

		if (foundedUser == null)
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

		var expiredTime =
			DateTime.UtcNow.AddMinutes(_applicationSettings.JwtSettings?.TokenExpiresTime ?? 15);

		var refreshToken = GenerateRefreshToken(ipAddress);

		refreshToken.UserId = foundedUser.Id;

		await _userRepository.AddUserLoginAsync(refreshToken);

		await _userRepository.SaveChangesAsync();

		var claims = GenerateClaims(new UserClaims
		{
			Id = foundedUser.Id.ToString(),
			RoleName = foundedUser.RoleName,
			RoleId = foundedUser.RoleId.ToString(),
			SecurityStamp = foundedUser.SecurityStamp.ToString(),
		});

		string token =
			_tokenServices.GenerateJwtToken
				(securityKey: _applicationSettings.JwtSettings?.SecretKeyForToken ?? string.Empty,
				claimsIdentity: claims,
				dateTime: expiredTime);

		await AddUserExistToCache(userId: foundedUser.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.LoginSuccessful);

		result.AddSuccessMessage(successMessage);

		LoginResponseViewModel response =
			new LoginResponseViewModel()
			{
				Token = token,
				Username = foundedUser.Username,
				RefreshToken = refreshToken.RefreshToken,
			};

		result.Value = response;

		_logger.LogWarning(Resources.Resource.UserLoginSuccessfulInformation, parameters: new List<object>
		{
			new
			{
				PhoneNumber = loginRequestViewModel.Username,
			}
		});

		return result;
	}


	public async Task<Result> ChangeUserRoleAsync
		(ChangeUserRoleRequestViewModel requestViewModel, int? adminId)
	{
		var result =
			ChangeUserRoleValidation(requestViewModel);

		if (result.IsFailed)
			return result;

		if (!adminId.HasValue)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.MostNotBeNull, nameof(adminId));

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var adminRoleId =
			await _userRepository.GetUserRoleAsync(adminId.Value);

		if (!adminRoleId.HasValue)
		{
			var errorMessage =
				string.Format(nameof(HttpStatusCode.Unauthorized));

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var isRoleExist =
			await _userRepository.CheckRoleExist(requestViewModel.RoleId);

		if (!isRoleExist)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.RoleNotFound);

			result.AddErrorMessage(errorMessage);

			return result;
		}

		var foundedUser =
			await _userRepository.GetUserById(userId: requestViewModel.UserId, isTracking: true);

		if (foundedUser == null)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.UserNotFound);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		if (foundedUser.RoleId == adminRoleId)
		{
			string errorMessage = string.Format
				(Resources.Messages.ErrorMessages.AccessDeniedForChangeRole);

			result.AddErrorMessage(errorMessage);
			return result;
		}

		if (adminRoleId == (int) UserRoleEnum.Admin)
		{
			if (foundedUser.RoleId == (int)UserRoleEnum.SystemAdministrator)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForChangeRole);

				result.AddErrorMessage(errorMessage);

				return result;
			}

			if (requestViewModel.RoleId == (int)UserRoleEnum.SystemAdministrator ||
				requestViewModel.RoleId == (int)UserRoleEnum.Admin)
			{
				string errorMessage = string.Format
					(Resources.Messages.ErrorMessages.AccessDeniedForChangeThisRole);

				result.AddErrorMessage(errorMessage);
				return result;
			}

		}

		foundedUser.RoleId = requestViewModel.RoleId;
		foundedUser.SecurityStamp = Guid.NewGuid();

		await _userRepository.SaveChangesAsync();

		await _cache.RemoveByPrefixAsync($"userId-{foundedUser.Id}");

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.UpdateSuccessful);

		result.AddSuccessMessage(successMessage);

		_logger.LogWarning(Resources.Resource.UpdateSuccessful, parameters: new List<object>
		{
			requestViewModel
		});

		return result;
	}


	public async Task<Result<LoginByOAuthResponseViewModel>> LoginByOAuthAsync
		(LoginByOAuthRequestViewModel requestViewModel, string? ipAddress)
	{
		var result =
			LoginByOAuthValidation(requestViewModel: requestViewModel);

		if (result.IsFailed == true)
			return result;

		var hashedPassword =
			Security.HashDataBySHA1(requestViewModel.Password);

		var foundedUser =
			await _userRepository.LoginAsync(username: requestViewModel.Username, hashedPassword: hashedPassword);

		if (foundedUser == null)
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

		var expiredTime =
			DateTime.UtcNow.AddMinutes(_applicationSettings.JwtSettings?.TokenExpiresTime ?? 15);

		var refreshToken = GenerateRefreshToken(ipAddress);

		refreshToken.UserId = foundedUser.Id;

		await _userRepository.AddUserLoginAsync(refreshToken);

		await _userRepository.SaveChangesAsync();

		var claims = GenerateClaims(new UserClaims
		{
			Id = foundedUser.Id.ToString(),
			RoleName = foundedUser.RoleName,
			RoleId = foundedUser.RoleId.ToString(),
			SecurityStamp = foundedUser.SecurityStamp.ToString(),
		});

		string token =
			_tokenServices.GenerateJwtToken
				(securityKey: _applicationSettings.JwtSettings?.SecretKeyForToken ?? string.Empty,
				claimsIdentity: claims,
				dateTime: expiredTime);

		await AddUserExistToCache(userId: foundedUser.Id);

		string successMessage = string.Format
			(Resources.Messages.SuccessMessages.LoginSuccessful);

		result.AddSuccessMessage(successMessage);

		var response =
			new LoginByOAuthResponseViewModel()
			{
				access_token = token,
				username = foundedUser.Username,
				refresh_token = refreshToken.RefreshToken.ToString(),
				token_type = "Bearer",
			};

		result.Value = response;

		_logger.LogWarning(Resources.Resource.UserLoginSuccessfulInformation, parameters: new List<object>
		{
			new
			{
				PhoneNumber = requestViewModel.Username,
			}
		});

		return result;
	}


	private ClaimsIdentity GenerateClaims(UserClaims userClaims)
	{
		var claims =
			new ClaimsIdentity(new[]
			{
				new Claim
					(type: ClaimTypes.NameIdentifier, value: userClaims.Id),

				new Claim
					(type: nameof(userClaims.RoleId), value: userClaims.RoleId),

				new Claim
					(type: ClaimTypes.Role, value: userClaims.RoleName),

				new Claim
					(type: nameof(userClaims.SecurityStamp), value: userClaims.SecurityStamp),
			});

		return claims;
	}
	#endregion /Methods
}
