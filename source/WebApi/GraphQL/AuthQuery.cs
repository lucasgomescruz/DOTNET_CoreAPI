using HotChocolate;
using Project.Application.Common.Interfaces;
using Project.Application.Common.Localizers;
using Project.Domain.Interfaces.Data.Repositories;
using Project.Domain.Interfaces.Services;

namespace Project.WebApi.GraphQL;

public sealed class AuthQuery
{
    private const int CacheMinutes = 30;

    [GraphQLDescription("Returns the profile of the currently authenticated user.")]
    public async Task<AuthenticatedUserType> GetAuthenticatedUser(
        [Service] IUser currentUser,
        [Service] IUserRepository userRepository,
        [Service] IRedisService redisService,
        [Service] CultureLocalizer localizer)
    {
        if (currentUser.Id is null)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(localizer.Text("LoginInvalidCredentials"))
                    .SetCode("UNAUTHENTICATED")
                    .Build());

        var cacheKey = $"AuthenticatedUser:{currentUser.Id}";
        var cached = await redisService.GetAsync<AuthenticatedUserType>(cacheKey);
        if (cached is not null) return cached;

        var user = userRepository.Get(x => x.Id == currentUser.Id);
        if (user is null)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("User not found.")
                    .SetCode("NOT_FOUND")
                    .Build());

        var payload = new AuthenticatedUserType(user.Id, user.Username, user.Email);
        await redisService.SetAsync(cacheKey, payload, TimeSpan.FromMinutes(CacheMinutes));
        return payload;
    }
}
