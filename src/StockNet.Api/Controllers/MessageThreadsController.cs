using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockNet.Api.Contracts.Messaging;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/message-threads")]
public sealed class MessageThreadsController(StockNetDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MessageThreadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageThreadDto>> Create(
        [FromBody] CreateMessageThreadRequest request,
        CancellationToken cancellationToken)
    {
        var buyerCompanyId = User.GetCompanyId();
        var now = DateTime.UtcNow;
        var stock = await dbContext.Stocks
            .AsNoTracking()
            .Where(item => item.Id == request.StockId)
            .Select(item => new
            {
                item.Id,
                SellerCompanyId = item.Warehouse.CompanyId,
                item.IsActive,
                item.AvailableFrom,
                item.AvailableTo
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (stock is null || !stock.IsActive || stock.AvailableFrom > now || stock.AvailableTo < now)
        {
            return NotFound();
        }

        if (stock.SellerCompanyId == buyerCompanyId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A company cannot start a message thread with itself."
            });
        }

        var alreadyExists = await dbContext.MessageThreads
            .AsNoTracking()
            .AnyAsync(
                thread => thread.StockId == stock.Id && thread.BuyerCompanyId == buyerCompanyId,
                cancellationToken);

        if (alreadyExists)
        {
            return DuplicateThread();
        }

        var messageThread = new MessageThread
        {
            StockId = stock.Id,
            BuyerCompanyId = buyerCompanyId,
            SellerCompanyId = stock.SellerCompanyId,
            CreatedAt = now
        };

        dbContext.MessageThreads.Add(messageThread);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateThread(exception))
        {
            return DuplicateThread();
        }
        catch (DbUpdateException exception) when (IsMissingStock(exception))
        {
            return NotFound();
        }

        var response = await GetParticipantThreadAsync(messageThread.Id, buyerCompanyId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = messageThread.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MessageThreadDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageThreadDto>>> ListMine(CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var threads = await dbContext.MessageThreads
            .AsNoTracking()
            .Where(thread => thread.BuyerCompanyId == companyId || thread.SellerCompanyId == companyId)
            .OrderByDescending(thread => thread.CreatedAt)
            .ThenBy(thread => thread.Id)
            .Select(MessageThreadDto.Projection)
            .ToListAsync(cancellationToken);

        return Ok(threads);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MessageThreadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageThreadDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var thread = await GetParticipantThreadAsync(id, companyId, cancellationToken);
        return thread is null ? NotFound() : Ok(thread);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType<MessagePageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessagePageDto>> ListMessages(
        Guid id,
        [FromQuery] MessagePageQuery query,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        if (!await IsParticipantAsync(id, companyId, cancellationToken))
        {
            return NotFound();
        }

        var messages = dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ThreadId == id);
        var totalCount = await messages.LongCountAsync(cancellationToken);
        var pageItems = await messages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MessageDto.Projection)
            .ToListAsync(cancellationToken);

        return Ok(new MessagePageDto(query.Page, query.PageSize, totalCount, pageItems));
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        if (!await IsParticipantAsync(id, companyId, cancellationToken))
        {
            return NotFound();
        }

        var message = new Message
        {
            ThreadId = id,
            SenderCompanyId = companyId,
            Text = request.Text.Trim(),
            SentAt = DateTime.UtcNow
        };

        dbContext.Messages.Add(message);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsMissingThread(exception))
        {
            return NotFound();
        }

        var response = await dbContext.Messages
            .AsNoTracking()
            .Where(item => item.Id == message.Id && item.ThreadId == id)
            .Select(MessageDto.Projection)
            .SingleAsync(cancellationToken);

        return CreatedAtAction(nameof(ListMessages), new { id }, response);
    }

    private Task<bool> IsParticipantAsync(Guid threadId, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.MessageThreads
            .AsNoTracking()
            .AnyAsync(
                thread => thread.Id == threadId
                    && (thread.BuyerCompanyId == companyId || thread.SellerCompanyId == companyId),
                cancellationToken);

    private Task<MessageThreadDto?> GetParticipantThreadAsync(
        Guid threadId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.MessageThreads
        .AsNoTracking()
        .Where(thread => thread.Id == threadId
            && (thread.BuyerCompanyId == companyId || thread.SellerCompanyId == companyId))
        .Select(MessageThreadDto.Projection)
        .SingleOrDefaultAsync(cancellationToken);

    private ConflictObjectResult DuplicateThread() => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "A message thread already exists for this company and stock."
    });

    private static bool IsDuplicateThread(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && postgresException.ConstraintName == "IX_MessageThreads_StockId_BuyerCompanyId";

    private static bool IsMissingStock(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation
        && postgresException.ConstraintName == "FK_MessageThreads_Stocks_StockId";

    private static bool IsMissingThread(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation
        && postgresException.ConstraintName == "FK_Messages_MessageThreads_ThreadId";
}
