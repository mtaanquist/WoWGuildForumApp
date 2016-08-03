using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlterEgo.Data;
using AlterEgo.Helpers;
using AlterEgo.Models;
using AlterEgo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlterEgo.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class BattleNetController : Controller
    {
        private const string GuildName = "Alter Ego";
        private const string Realm = "Argent Dawn";

        private readonly ApplicationDbContext _context;
        private readonly BattleNetApi _battleNetApi;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<BattleNetOptions> _options;

        public BattleNetController(ApplicationDbContext context, BattleNetApi battleNetApi, UserManager<ApplicationUser> userManager, IOptions<BattleNetOptions> options)
        {
            _context = context;
            _battleNetApi = battleNetApi;
            _userManager = userManager;
            _options = options;
        }

        [Route("[action]")]
        [HttpGet]
        public async Task<IActionResult> UpdateNews()
        {
            var news = await _battleNetApi.GetGuildNews(Realm, GuildName);

            var currentNewsEntries = _context.News.ToList();
            news.RemoveAll(x => !currentNewsEntries.Contains(x));

            _context.News.AddRange(news);
            var result = await _context.SaveChangesAsync();

            return Json(new ApiStatusMessage { StatusCode = 200, Changes = result });
        }

        [Route("[action]")]
        [HttpGet]
        public async Task<IActionResult> UpdateUserCharacters()
        {
            var users = _context.Users.ToList();
            var storedCharacters = _context.Characters.AsNoTracking().ToList();
            
            var characters = new List<Character>();
            await users.ForEachAsync(async user =>
            {
                if (!string.IsNullOrEmpty(user.AccessToken))
                {
                    try
                    {
                        var userCharacters = await _battleNetApi.GetUserCharacters(user.AccessToken);

                        if (userCharacters != null)
                        {
                            userCharacters.ForEach(c =>
                            {
                                c.User = user;
                                c.UserId = user.Id;
                                c.CharacterRace = _context.Races.SingleOrDefault(r => r.Id == c.Race);
                                c.CharacterClass = _context.Classes.SingleOrDefault(cl => cl.Id == c.Class);
                            });
                            characters.AddRange(userCharacters);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        user.AccessToken = "";
                        user.AccessTokenExpiry = "";
                        await _userManager.UpdateAsync(user);
                    }
                }
            });

            // Add, update or delete characters in the stored list
            var newCharacters =
                characters.Where(c => !storedCharacters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            _context.AddRange(newCharacters);

            var removedCharacters =
                storedCharacters.Where(c => !characters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            removedCharacters.RemoveAll(c => c.UserId == null);
            _context.RemoveRange(removedCharacters);

            var changedCharacters =
                characters.Where(c => storedCharacters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            changedCharacters.RemoveAll(c => c.UserId == null);
            _context.UpdateRange(changedCharacters);

            await _context.SaveChangesAsync();

            return Ok();
        }

        [Route("[action]")]
        [HttpGet]
        public async Task<IActionResult> UpdateGuildRoster()
        {
            var roster = await _battleNetApi.GetGuildRoster(Realm, GuildName);

            var characters = new List<Character>();
            roster.ForEach(member => characters.Add(member.Character));

            characters.ForEach(c =>
            {
                c.CharacterRace = _context.Races.SingleOrDefault(r => r.Id == c.Race);
                c.CharacterClass = _context.Classes.SingleOrDefault(cl => cl.Id == c.Class);
            });

            var storedCharacters = _context.Characters.Include(c => c.User).AsNoTracking().ToList();

            // Add, update or delete characters in the stored list
            var newCharacters =
                characters.Where(c => !storedCharacters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            _context.AddRange(newCharacters);

            var removedCharacters =
                storedCharacters.Where(c => !characters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            removedCharacters.RemoveAll(c => c.User != null);
            _context.RemoveRange(removedCharacters);

            var changedCharacters =
                characters.Where(c => storedCharacters.Any(x => c.Name == x.Name && c.Realm == x.Realm))
                    .ToList();
            changedCharacters.RemoveAll(c => c.User != null);
            _context.UpdateRange(changedCharacters);

            await _context.SaveChangesAsync();

            // Update the Member table
            var members = new List<Member>();
            await roster.ForEachAsync(async r =>
            {
                var member = new Member
                {
                    Character = await _context.Characters.SingleOrDefaultAsync(c => c.Name == r.Character.Name && c.Realm == r.Character.Realm),
                    CharacterName = r.Character.Name,
                    CharacterRealm = r.Character.Realm,
                    Rank = r.Rank
                };

                members.Add(member);
            });

            var storedMembers = await _context.Members.Include(m => m.Character).AsNoTracking().ToListAsync();

            // Add, update or delete members in the stored list
            var newMembers =
                members.Where(c => !storedMembers.Any(x => c.CharacterName == x.CharacterName && c.CharacterRealm == x.CharacterRealm))
                    .ToList();
            _context.AddRange(newMembers);

            var removedMembers =
                storedMembers.Where(c => !members.Any(x => c.CharacterName == x.CharacterName && c.CharacterRealm == x.CharacterRealm))
                    .ToList();
            _context.RemoveRange(removedMembers);

            var changedMembers =
                members.Where(c => storedMembers.Any(x => c.CharacterName == x.CharacterName && c.CharacterRealm == x.CharacterRealm))
                    .ToList();
            _context.UpdateRange(changedMembers);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [Route("[action]")]
        [HttpGet]
        public async Task<IActionResult> UpdateGuildRanks()
        {
            // Before running this, UpdateUsersCharacters and UpdateGuildRoster must have run first.
            var users = await _context.Users.ToListAsync();
            users.ForEach(user =>
            {
                var currentRank = (int)GuildRank.Everyone;
                var characters = _context.Characters.Where(c => c.User == user && c.Realm == Realm && c.Guild == GuildName).ToList();
                characters.ForEach(character =>
                {
                    var member = _context.Members.SingleOrDefault(m => m.CharacterName == character.Name && m.CharacterRealm == character.Realm);
                    if (member != null)
                        currentRank = (member.Rank < currentRank) ? member.Rank : currentRank;
                });

                user.Rank = currentRank;
            });

            _context.UpdateRange(users);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}