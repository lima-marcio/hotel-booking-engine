import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { createRoom, getRoom, updateRoom } from "../features/rooms/roomService";

const roomSchema = z.object({
  roomNumber: z.string().min(1, "Room number is required").max(20),
  status: z.enum(["Available", "Maintenance"]),
});

type RoomFormValues = z.infer<typeof roomSchema>;

export function RoomFormPage() {
  const {
    hotelId: hotelIdParam,
    roomTypeId: roomTypeIdParam,
    id,
  } = useParams<{ hotelId: string; roomTypeId: string; id: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = Number(roomTypeIdParam);
  const roomId = id ? Number(id) : undefined;
  const isEditMode = roomId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [saveError, setSaveError] = useState<string | null>(null);

  const { data: existingRoom } = useQuery({
    queryKey: ["rooms", roomId],
    queryFn: () => getRoom(roomId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RoomFormValues>({
    resolver: zodResolver(roomSchema),
    defaultValues: { status: "Available" },
  });

  useEffect(() => {
    if (existingRoom && isEditMode) {
      if (existingRoom.roomTypeId !== roomTypeId) {
        navigate(`/hotels/${existingRoom.hotelId}/room-types/${existingRoom.roomTypeId}/rooms`, { replace: true });
        return;
      }

      reset({
        roomNumber: existingRoom.roomNumber,
        status: existingRoom.status,
      });
    }
  }, [existingRoom, isEditMode, roomTypeId, hotelId, navigate, reset]);

  const mutation = useMutation({
    mutationFn: (values: RoomFormValues) =>
      isEditMode ? updateRoom(roomId!, values) : createRoom(roomTypeId, values),
    onSuccess: () => {
      setSaveError(null);
      queryClient.invalidateQueries({ queryKey: ["room-types", roomTypeId, "rooms"] });
      if (roomId !== undefined) {
        queryClient.invalidateQueries({ queryKey: ["rooms", roomId] });
      }
      navigate(`/hotels/${hotelId}/room-types/${roomTypeId}/rooms`);
    },
    onError: (error) => {
      const message =
        isAxiosError(error) && typeof error.response?.data === "string"
          ? error.response.data
          : "Unable to save this room.";
      setSaveError(message);
    },
  });

  function onSubmit(values: RoomFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Room" : "New Room"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="roomNumber" className="block text-sm font-medium">
            Room Number
          </label>
          <input
            id="roomNumber"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("roomNumber")}
          />
          {errors.roomNumber && <p className="text-sm text-red-600">{errors.roomNumber.message}</p>}
        </div>
        <div>
          <label htmlFor="status" className="block text-sm font-medium">
            Status
          </label>
          <select id="status" className="w-full rounded border px-3 py-2" {...register("status")}>
            <option value="Available">Available</option>
            <option value="Maintenance">Maintenance</option>
          </select>
          {errors.status && <p className="text-sm text-red-600">{errors.status.message}</p>}
        </div>
        {saveError && <p className="text-sm text-red-600">{saveError}</p>}
        <button
          type="submit"
          disabled={mutation.isPending}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {mutation.isPending ? "Saving..." : "Save"}
        </button>
      </form>
    </main>
  );
}
