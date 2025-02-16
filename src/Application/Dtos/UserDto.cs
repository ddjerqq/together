using AutoMapper;
using Domain.Aggregates;
using Astro.Generated;

namespace Application.Dtos;

[AutoMap(typeof(User))]
public sealed class UserDto
{
    public UserId Id { get; init; }

    public string Username { get; init; } = default!;

    public decimal Balance { get; init; }
}