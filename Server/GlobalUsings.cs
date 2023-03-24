﻿global using AutoMapper;
global using Domain.UserManagment;
global using Dtat.Logging;
global using Dtat.Logging.NLogAdapter;
global using Dtat.Result;
global using EasyCaching.Core;
global using Infrastructure;
global using Infrastructure.Attributes;
global using Infrastructure.Enums;
global using Infrastructure.Extentions;
global using Infrastructure.Middlewares;
global using Infrastructure.Settings;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.Filters;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.Tokens;
global using Persistence;
global using Persistence.Repositories;
global using Services;
global using Shared.DICommon;
global using Softmax.Swagger;
global using Softmax.Utilities.Validation;
global using SwaggerAuthorization.Extensions;
global using System;
global using System.Collections.Generic;
global using System.IdentityModel.Tokens.Jwt;
global using System.IO;
global using System.Linq;
global using System.Net;
global using System.Security.Claims;
global using System.Text;
global using System.Text.Json.Serialization;
global using System.Threading.Tasks;
global using ViewModels.General;

