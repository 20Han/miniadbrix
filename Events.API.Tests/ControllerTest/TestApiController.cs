using System;
using Events.API.Controllers;
using Events.API.Dto;
using Events.API.Entities;
using Events.API.Repository;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Events.API.Tests.ControllerTest;

public class TestApiController
{
    /*
     * ApiController의 CollectItem API가 성공했을 경우를 테스트한다
     */
    [Fact]
    public async Task CollectItem_Success()
    {
        // Arrange
        var mockEventRepository = new Mock<IEventRepository>();

        mockEventRepository
            .Setup(repository => repository.SendSQS(It.IsAny<String>(), It.IsAny<String>()))
            .ReturnsAsync(true);

        var sut = new ApiController(mockEventRepository.Object);

        // Act
        var result = await sut.CollectItem(new CollectEventDto
        {
            EventId = Guid.NewGuid(),
            EventName = "test_event",
            Parameters = new Parameters
            {
                OrderId = Guid.NewGuid(),
                Currency = "KRW",
                Price = 1000
            },
            UserId = Guid.NewGuid()
        });

        // Assert
        result.IsSuccess.Should().Be("true");
    }

    /*
     * ApiController의 CollectItem API가 실했을 경우를 테스트한다
     */
    [Fact]
    public async Task CollectItem_Fail()
    {
        // Arrange
        var mockEventRepository = new Mock<IEventRepository>();

        mockEventRepository
            .Setup(repository => repository.SendSQS(It.IsAny<String>(), It.IsAny<String>()))
            .ReturnsAsync(false);

        var sut = new ApiController(mockEventRepository.Object);

        // Act
        var result = await sut.CollectItem(new CollectEventDto
        {
            EventId = Guid.NewGuid(),
            EventName = "test_event",
            Parameters = new Parameters
            {
                OrderId = Guid.NewGuid(),
                Currency = "KRW",
                Price = 1000
            },
            UserId = Guid.NewGuid()
        });

        // Assert
        result.IsSuccess.Should().Be("false");
    }

    /*
     * ApiController의 SearchEvent API가 성공했을 경우를 테스트한다
     */
    [Fact]
    public async Task SearchEvent_Success()
    {
        // Arrange
        var mockEventRepository = new Mock<IEventRepository>();
        var eventList = new List<Event> {
                new Event {
                    EventId = Guid.NewGuid(),
                    CreateDate = DateTimeOffset.Now,
                    EventName = "test_event1",
                    Parameters = null,
                    UserId = Guid.NewGuid()
                },
                new Event {
                    EventId = Guid.NewGuid(),
                    CreateDate = DateTimeOffset.Now,
                    EventName = "test_event2",
                    Parameters = null,
                    UserId = Guid.NewGuid()
                }
            };

        mockEventRepository
            .Setup(repository => repository.GetEvent(It.IsAny<Guid>()))
            .ReturnsAsync(eventList);

        var sut = new ApiController(mockEventRepository.Object);

        // Act
        var result = await sut.SearchItem(new SearchEventDto
        {
            UserId = Guid.NewGuid()
        });

        // Assert
        result.IsSuccess.Should().Be("true");
        result.Results.Count().Should().Be(2);
    }

    /*
     * repository.GetEvent에서 에러가 발생해 ApiController의 SearchEvent API가 실패했을 경우를 테스트한다
     */
    [Fact]
    public async Task SearchEvent_Fail()
    {
        // Arrange
        var mockEventRepository = new Mock<IEventRepository>();

        mockEventRepository
            .Setup(repository => repository.GetEvent(It.IsAny<Guid>()))
            .ThrowsAsync(new Exception());

        var sut = new ApiController(mockEventRepository.Object);

        // Act
        var result = await sut.SearchItem(new SearchEventDto
        {
            UserId = Guid.NewGuid()
        });

        // Assert
        result.IsSuccess.Should().Be("false");
        result.Results.Count().Should().Be(0);
    }
}

