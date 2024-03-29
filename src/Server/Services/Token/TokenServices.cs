﻿namespace Services;

public class TokenServices : ITokenServices, IRegisterAsScoped
{
	#region MainMethods
	/// <summary>
	/// Generate jwt access token
	/// </summary>
	public string GenerateJwtToken
		(string securityKey, ClaimsIdentity claimsIdentity, DateTimeOffset dateTime)
	{
		Assert.NotEmpty(securityKey, nameof(securityKey));

		var signingCredentional =
			GenerateSigningCredentional(securityKey!);

		var tokenDescriptor =
			GenerateTokenDescriptor(claimsIdentity, dateTime.UtcDateTime, signingCredentional);

		var tokenHandler = new JwtSecurityTokenHandler();

		SecurityToken securityToken =
			tokenHandler.CreateToken(tokenDescriptor);

		string token =
			tokenHandler.WriteToken(securityToken);

		return token;
	}
	#endregion /MainMethods

	#region SubMethods
	private SigningCredentials GenerateSigningCredentional(string securityKey)
	{
		byte[] key =
			Encoding.ASCII.GetBytes(securityKey);

		var symmetricSecurityKey =
			new SymmetricSecurityKey(key: key);

		var securityAlgorithm =
			SecurityAlgorithms.HmacSha256Signature;

		var signingCredentional =
			new SigningCredentials(key: symmetricSecurityKey, algorithm: securityAlgorithm);

		return signingCredentional;
	}


	private SecurityTokenDescriptor GenerateTokenDescriptor
		(ClaimsIdentity claimsIdentity, DateTime dateTime, SigningCredentials signingCredentional)
	{
		var tokenDescriptor =
			new SecurityTokenDescriptor()
			{
				Subject = claimsIdentity,

				//Issuer = "",
				//Audience = "",

				Expires = dateTime,
				SigningCredentials = signingCredentional,
			};

		return tokenDescriptor;
	}
	#endregion /SubMethods
}
